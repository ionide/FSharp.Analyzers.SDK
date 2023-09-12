import {LitElement, html, css} from 'https://cdn.jsdelivr.net/gh/lit/dist@2/core/lit-core.min.js';
import copy from 'https://esm.sh/copy-to-clipboard@3.3.3';

export class CopyCommandButton extends LitElement {
    static properties = {
        content: {type: String, attribute: true},
        clicked: {type: Boolean, state: true}
    }

    constructor() {
        super();
        this.clicked = false;
    }

    static styles = css`
      iconify-icon {
        cursor: pointer;
      }
    `

    onClick() {
        copy(this.content);
        this.clicked = true;
        setTimeout( () => {
            this.clicked = false;
        }, 500);
    }

    render() {
        return this.clicked ? html`
            <iconify-icon icon="ic:twotone-check" width="16" height="16"></iconify-icon>` : html`
            <iconify-icon icon="solar:clipboard-outline" width="16" height="16" @click=${this.onClick}></iconify-icon>`;
    }
}

customElements.define('copy-icon', CopyCommandButton);

const codeToCopy = [...document.querySelectorAll("code[lang=shell],code[lang=bash]")];
codeToCopy.forEach(code => {
    const copyIcon = document.createElement("copy-icon");
    copyIcon.setAttribute("content", code.textContent);
    const wrapInTd = element => { const td = document.createElement("td"); td.append(element); return td }
    const row = code.parentElement.parentElement.parentElement;
    row.append(wrapInTd(copyIcon));
    
    const terminalIcon = document.createElement("iconify-icon");
    terminalIcon.setAttribute("icon", "ph:terminal-bold");
    terminalIcon.setAttribute("width", "16");
    terminalIcon.setAttribute("height", "16");
    row.prepend(wrapInTd(terminalIcon));
});