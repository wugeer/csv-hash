import { invoke } from "@tauri-apps/api/core";
import { open } from "@tauri-apps/plugin-dialog";
import "./styles.css";

const delimiterOptions = [
  { label: "逗号", value: "," },
  { label: "分号", value: ";" },
  { label: "竖线", value: "|" },
  { label: "制表符", value: "\t" },
];

const state = {
  csvPath: "",
  delimiter: ",",
  headers: [],
  selectedFields: new Set(),
  busy: false,
  message: "",
  messageType: "idle",
  outputPath: "",
};

const app = document.querySelector("#app");

function render() {
  app.innerHTML = `
    <section class="shell">
      <aside class="summary-panel">
        <p class="eyebrow">CSV Hash Client</p>
        <h1>字段脱敏工作台</h1>
        <div class="summary-grid">
          <div>
            <span>文件</span>
            <strong>${state.csvPath ? getFileName(state.csvPath) : "未选择"}</strong>
          </div>
          <div>
            <span>字段</span>
            <strong>${state.selectedFields.size}</strong>
          </div>
          <div>
            <span>输出</span>
            <strong>entry-原文件名</strong>
          </div>
        </div>
      </aside>

      <section class="workspace">
        <div class="toolbar">
          <button class="primary-button" id="select-file" type="button" ${state.busy ? "disabled" : ""}>
            选择 CSV 文件
          </button>
          <div class="delimiter-control" role="group" aria-label="CSV 分隔符">
            ${delimiterOptions
              .map(
                (option) => `
                  <button
                    class="delimiter-chip ${state.delimiter === option.value ? "active" : ""}"
                    data-delimiter="${encodeURIComponent(option.value)}"
                    type="button"
                    ${state.busy ? "disabled" : ""}
                  >
                    ${option.label}
                  </button>
                `,
              )
              .join("")}
          </div>
          <label class="custom-delimiter">
            <span>自定义</span>
            <input
              id="delimiter-input"
              maxlength="2"
              value="${escapeHtml(displayDelimiter(state.delimiter))}"
              ${state.busy ? "disabled" : ""}
            />
          </label>
        </div>

        <div class="file-strip ${state.csvPath ? "loaded" : ""}">
          <span>${state.csvPath ? escapeHtml(state.csvPath) : "请选择一个带表头的 CSV 文件"}</span>
        </div>

        <section class="field-panel">
          <div class="field-panel-heading">
            <div>
              <p class="section-label">加密字段</p>
              <h2>勾选需要 SHA-256 脱敏的列</h2>
            </div>
            <div class="field-actions">
              <button class="ghost-button" id="select-all" type="button" ${!state.headers.length || state.busy ? "disabled" : ""}>
                全选
              </button>
              <button class="ghost-button" id="clear-all" type="button" ${!state.headers.length || state.busy ? "disabled" : ""}>
                清空
              </button>
            </div>
          </div>

          <div class="fields-grid">
            ${
              state.headers.length
                ? state.headers
                    .map(
                      (header) => `
                        <label class="field-tile">
                          <input
                            type="checkbox"
                            value="${escapeHtml(header)}"
                            ${state.selectedFields.has(header) ? "checked" : ""}
                            ${state.busy ? "disabled" : ""}
                          />
                          <span>${escapeHtml(header)}</span>
                        </label>
                      `,
                    )
                    .join("")
                : `<div class="empty-state">表头会在选择文件后显示</div>`
            }
          </div>
        </section>

        <footer class="action-bar">
          <button
            class="run-button"
            id="encrypt"
            type="button"
            ${!canEncrypt() || state.busy ? "disabled" : ""}
          >
            ${state.busy ? "处理中..." : "生成加密 CSV"}
          </button>
          <div class="status ${state.messageType}">
            <span>${escapeHtml(state.message)}</span>
            ${state.outputPath ? `<strong>${escapeHtml(state.outputPath)}</strong>` : ""}
          </div>
        </footer>
      </section>
    </section>
  `;

  bindEvents();
}

function bindEvents() {
  document.querySelector("#select-file").addEventListener("click", selectFile);
  document.querySelector("#encrypt").addEventListener("click", encrypt);
  document.querySelector("#select-all").addEventListener("click", () => {
    state.selectedFields = new Set(state.headers);
    render();
  });
  document.querySelector("#clear-all").addEventListener("click", () => {
    state.selectedFields.clear();
    render();
  });

  document.querySelectorAll(".delimiter-chip").forEach((button) => {
    button.addEventListener("click", async () => {
      state.delimiter = decodeURIComponent(button.dataset.delimiter);
      await reloadHeaders();
    });
  });

  const delimiterInput = document.querySelector("#delimiter-input");
  delimiterInput.addEventListener("change", () => updateCustomDelimiter(delimiterInput.value));
  delimiterInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      delimiterInput.blur();
    }
  });

  document.querySelectorAll(".field-tile input").forEach((checkbox) => {
    checkbox.addEventListener("change", (event) => {
      if (event.target.checked) {
        state.selectedFields.add(event.target.value);
      } else {
        state.selectedFields.delete(event.target.value);
      }
      render();
    });
  });
}

async function selectFile() {
  const selected = await open({
    multiple: false,
    filters: [{ name: "CSV", extensions: ["csv", "txt"] }],
  });

  if (!selected) {
    return;
  }

  state.csvPath = selected;
  state.selectedFields.clear();
  state.outputPath = "";
  await reloadHeaders();
}

async function reloadHeaders() {
  if (!state.csvPath) {
    render();
    return;
  }

  setBusy(true, "正在读取表头...", "idle");

  try {
    const headers = await invoke("read_headers", {
      csvPath: state.csvPath,
      delimiter: state.delimiter,
    });
    state.headers = headers;
    state.selectedFields = new Set([...state.selectedFields].filter((field) => headers.includes(field)));
    state.message = `已读取 ${headers.length} 个字段`;
    state.messageType = "success";
  } catch (error) {
    state.headers = [];
    state.selectedFields.clear();
    state.message = String(error);
    state.messageType = "error";
  } finally {
    state.busy = false;
    render();
  }
}

async function encrypt() {
  setBusy(true, "正在生成加密文件...", "idle");

  try {
    const result = await invoke("encrypt_csv_file", {
      csvPath: state.csvPath,
      fields: [...state.selectedFields],
      delimiter: state.delimiter,
    });
    state.outputPath = result.output_path;
    state.message = `已加密 ${result.encrypted_fields.length} 个字段`;
    state.messageType = "success";
  } catch (error) {
    state.outputPath = "";
    state.message = String(error);
    state.messageType = "error";
  } finally {
    state.busy = false;
    render();
  }
}

function setBusy(busy, message, messageType) {
  state.busy = busy;
  state.message = message;
  state.messageType = messageType;
  render();
}

async function updateCustomDelimiter(value) {
  const nextDelimiter = parseDelimiterInput(value.trim());
  state.delimiter = nextDelimiter || ",";
  await reloadHeaders();
}

function canEncrypt() {
  return state.csvPath && state.headers.length && state.selectedFields.size;
}

function displayDelimiter(delimiter) {
  return delimiter === "\t" ? "\\t" : delimiter;
}

function parseDelimiterInput(value) {
  return value === "\\t" ? "\t" : value;
}

function getFileName(path) {
  return path.split(/[\\/]/).pop();
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

render();
