window.ibDownload = (fileName, contentType, text) => {
    // CSV gets a UTF-8 byte order mark so Excel opens names and "ZiG" correctly (spec §4.2).
    const parts = contentType.startsWith('text/csv') ? ['﻿', text] : [text];
    const blob = new Blob(parts, { type: contentType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
};
