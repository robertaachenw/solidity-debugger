const buttonOpenFolder = 'button-open-folder';
const inputOpenFolder = 'input-open-folder';

const vscode = acquireVsCodeApi();

window.addEventListener("load", onLoad);

function onLoad() {
    for (let i = 0; ; i++) {
        let lnkName = 'link-' + i;
        let lnkObject = document.getElementById(lnkName);
        if (!lnkObject) {
            break;
        }
        lnkObject.addEventListener("click", () => {
            linkOnClick(lnkName);
        });
    }

    document.getElementById(buttonOpenFolder).addEventListener("click", buttonOpenFolderOnClick);

    setVSCodeMessageListener();
}


function linkOnClick(sender) {
    let lnk = document.getElementById(sender);

    vscode.postMessage({
        command: 'link', path: lnk.href
    });
}


function buttonOpenFolderOnClick(sender) {
    vscode.postMessage({
        command: 'openFolder', path: document.getElementById(inputOpenFolder).value
    });
}


function setVSCodeMessageListener() {
    window.addEventListener("message", (event) => {
    });
}

