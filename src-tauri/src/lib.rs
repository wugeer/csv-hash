use std::collections::HashSet;
use std::path::{Path, PathBuf};

use csv::{ReaderBuilder, StringRecord, WriterBuilder};
use serde::Serialize;
use sha2::{Digest, Sha256};

#[derive(Debug, Serialize)]
struct EncryptionResult {
    output_path: String,
    encrypted_fields: Vec<String>,
}

#[tauri::command]
fn read_headers(csv_path: String, delimiter: String) -> Result<Vec<String>, String> {
    let delimiter = parse_delimiter(&delimiter)?;
    read_csv_headers(Path::new(&csv_path), delimiter)
}

#[tauri::command]
fn encrypt_csv_file(
    csv_path: String,
    fields: Vec<String>,
    delimiter: String,
) -> Result<EncryptionResult, String> {
    let delimiter = parse_delimiter(&delimiter)?;
    let fields = parse_fields(fields)?;
    let output_path = output_path(Path::new(&csv_path))?;

    encrypt_csv(Path::new(&csv_path), &output_path, &fields, delimiter)?;

    let mut encrypted_fields: Vec<_> = fields.into_iter().collect();
    encrypted_fields.sort();

    Ok(EncryptionResult {
        output_path: output_path.display().to_string(),
        encrypted_fields,
    })
}

pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .invoke_handler(tauri::generate_handler![read_headers, encrypt_csv_file])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}

fn parse_delimiter(delimiter: &str) -> Result<u8, String> {
    let delimiter = delimiter.as_bytes();
    match delimiter {
        [byte] => Ok(*byte),
        _ => Err("分隔符必须是 1 个字节字符，例如 , | ; 或制表符".to_owned()),
    }
}

fn output_path(input_path: &Path) -> Result<PathBuf, String> {
    let file_name = input_path
        .file_name()
        .ok_or_else(|| "CSV 路径必须包含文件名".to_owned())?;
    let output_file_name = format!("entry-{}", file_name.to_string_lossy());

    Ok(input_path.with_file_name(output_file_name))
}

fn parse_fields(fields: Vec<String>) -> Result<HashSet<String>, String> {
    let fields: HashSet<String> = fields
        .into_iter()
        .map(|field| field.trim().to_owned())
        .filter(|field| !field.is_empty())
        .collect();

    if fields.is_empty() {
        return Err("请选择至少一个要加密的字段".to_owned());
    }

    Ok(fields)
}

fn read_csv_headers(input_path: &Path, delimiter: u8) -> Result<Vec<String>, String> {
    let mut reader = ReaderBuilder::new()
        .delimiter(delimiter)
        .from_path(input_path)
        .map_err(|err| format!("读取 CSV 失败: {err}"))?;

    let headers = reader
        .headers()
        .map_err(|err| format!("读取 CSV 表头失败: {err}"))?;

    Ok(headers.iter().map(ToOwned::to_owned).collect())
}

fn encrypt_csv(
    input_path: &Path,
    output_path: &Path,
    fields: &HashSet<String>,
    delimiter: u8,
) -> Result<(), String> {
    let mut reader = ReaderBuilder::new()
        .delimiter(delimiter)
        .from_path(input_path)
        .map_err(|err| format!("读取 CSV 失败: {err}"))?;

    let headers = reader
        .headers()
        .map_err(|err| format!("读取 CSV 表头失败: {err}"))?
        .clone();
    let encrypted_indexes = encrypted_indexes(&headers, fields)?;

    let mut writer = WriterBuilder::new()
        .delimiter(delimiter)
        .from_path(output_path)
        .map_err(|err| format!("创建输出文件失败: {err}"))?;

    writer
        .write_record(&headers)
        .map_err(|err| format!("写入 CSV 表头失败: {err}"))?;

    for record in reader.records() {
        let record = record.map_err(|err| format!("读取 CSV 记录失败: {err}"))?;
        let encrypted_record = encrypt_record(&record, &encrypted_indexes);
        writer
            .write_record(&encrypted_record)
            .map_err(|err| format!("写入 CSV 记录失败: {err}"))?;
    }

    writer
        .flush()
        .map_err(|err| format!("保存输出文件失败: {err}"))?;
    Ok(())
}

fn encrypted_indexes(
    headers: &StringRecord,
    fields: &HashSet<String>,
) -> Result<HashSet<usize>, String> {
    let mut missing_fields = fields.clone();
    let mut indexes = HashSet::new();

    for (index, header) in headers.iter().enumerate() {
        if fields.contains(header) {
            indexes.insert(index);
            missing_fields.remove(header);
        }
    }

    if !missing_fields.is_empty() {
        let mut missing_fields: Vec<_> = missing_fields.into_iter().collect();
        missing_fields.sort();
        return Err(format!(
            "CSV 表头中不存在字段: {}",
            missing_fields.join(",")
        ));
    }

    Ok(indexes)
}

fn encrypt_record(record: &StringRecord, encrypted_indexes: &HashSet<usize>) -> StringRecord {
    record
        .iter()
        .enumerate()
        .map(|(index, value)| {
            if encrypted_indexes.contains(&index) {
                sha256_hex(value)
            } else {
                value.to_owned()
            }
        })
        .collect()
}

fn sha256_hex(value: &str) -> String {
    let digest = Sha256::digest(value.as_bytes());
    format!("{digest:x}")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn defaults_to_single_byte_delimiter() {
        assert_eq!(parse_delimiter(",").unwrap(), b',');
        assert_eq!(parse_delimiter("|").unwrap(), b'|');
        assert_eq!(parse_delimiter("\t").unwrap(), b'\t');
        assert!(parse_delimiter("||").is_err());
    }

    #[test]
    fn builds_entry_output_path_next_to_input_file() {
        let path = output_path(Path::new("/tmp/users.csv")).unwrap();
        assert_eq!(path, PathBuf::from("/tmp/entry-users.csv"));
    }

    #[test]
    fn resolves_encrypted_indexes_from_header_names() {
        let headers = StringRecord::from(vec!["id", "name", "phone"]);
        let fields = parse_fields(vec!["name".to_owned(), "phone".to_owned()]).unwrap();
        let indexes = encrypted_indexes(&headers, &fields).unwrap();

        assert!(indexes.contains(&1));
        assert!(indexes.contains(&2));
        assert_eq!(indexes.len(), 2);
    }

    #[test]
    fn fails_when_requested_field_is_missing() {
        let headers = StringRecord::from(vec!["id", "name"]);
        let fields = parse_fields(vec!["phone".to_owned()]).unwrap();

        assert!(encrypted_indexes(&headers, &fields).is_err());
    }

    #[test]
    fn encrypts_only_selected_record_values() {
        let record = StringRecord::from(vec!["1", "alice", "13800138000"]);
        let encrypted_indexes = HashSet::from([1, 2]);
        let encrypted_record = encrypt_record(&record, &encrypted_indexes);

        assert_eq!(encrypted_record.get(0), Some("1"));
        assert_eq!(encrypted_record.get(1), Some(sha256_hex("alice").as_str()));
        assert_eq!(
            encrypted_record.get(2),
            Some(sha256_hex("13800138000").as_str())
        );
    }
}
