let currentTip = null;
let currentTipElement = null;

function hideTip(evt, name, unique) {
    const el = document.getElementById(name);
    el.style.display = "none";
    currentTip = null;
}

function findPos(obj) {
    // no idea why, but it behaves differently in webbrowser component
    if (window.location.search === "?inapp")
        return [obj.offsetLeft + 10, obj.offsetTop + 30];

    let curleft = 0;
    let curtop = obj.offsetHeight;
    while (obj) {
        curleft += obj.offsetLeft;
        curtop += obj.offsetTop;
        obj = obj.offsetParent;
    }
    return [curleft, curtop];
}

function hideUsingEsc(e) {
    hideTip(e, currentTipElement, currentTip);
}

function showTip(evt, name, unique, owner) {
    document.onkeydown = hideUsingEsc;
    if (currentTip === unique) return;
    currentTip = unique;
    currentTipElement = name;

    let pos = findPos(owner ? owner : (evt.srcElement ? evt.srcElement : evt.target));
    const posx = pos[0];
    const posy = pos[1];

    const el = document.getElementById(name);
    el.style.position = "absolute";
    el.style.left = posx + "px";
    el.style.top = posy + "px";
    el.style.display = "block";
}

function Clipboard_CopyTo(value) {
    const tempInput = document.createElement("input");
    tempInput.value = value;
    document.body.appendChild(tempInput);
    tempInput.select();
    document.execCommand("copy");
    document.body.removeChild(tempInput);
}

window.showTip = showTip;
window.hideTip = hideTip;
// Used by API documentation
window.Clipboard_CopyTo = Clipboard_CopyTo;