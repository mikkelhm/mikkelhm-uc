(function () {
    'use strict';

    var STORAGE_KEY = 'ellabm_auth';
    var API_UPLOAD = '/umbraco/api/ellabm/upload';
    var MAX_SIZE = 2048;

    var gate = document.getElementById('password-gate');
    var section = document.getElementById('upload-section');
    var passwordInput = document.getElementById('password-input');
    var passwordSubmit = document.getElementById('password-submit');
    var passwordError = document.getElementById('password-error');
    var dropZone = document.getElementById('drop-zone');
    var fileInput = document.getElementById('file-input');
    var uploaderName = document.getElementById('uploader-name');
    var previewArea = document.getElementById('preview-area');
    var progressArea = document.getElementById('progress-area');
    var progressFill = document.getElementById('progress-fill');
    var progressText = document.getElementById('progress-text');
    var uploadStatus = document.getElementById('upload-status');

    var selectedFiles = [];
    var password = '';

    // Check stored password
    function init() {
        var stored = localStorage.getItem(STORAGE_KEY);
        if (stored) {
            password = stored;
            showUpload();
        }

        // Restore uploader name
        var savedName = localStorage.getItem('ellabm_name');
        if (savedName) {
            uploaderName.value = savedName;
        }
    }

    function showUpload() {
        gate.classList.add('hidden');
        section.classList.remove('hidden');
    }

    // Password gate
    passwordSubmit.addEventListener('click', function () {
        tryPassword();
    });

    passwordInput.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') tryPassword();
    });

    function tryPassword() {
        var val = passwordInput.value.trim();
        if (val === '2026') {
            password = val;
            localStorage.setItem(STORAGE_KEY, val);
            passwordError.classList.add('hidden');
            showUpload();
        } else {
            passwordError.classList.remove('hidden');
            passwordInput.value = '';
            passwordInput.focus();
        }
    }

    // Drag and drop
    dropZone.addEventListener('dragover', function (e) {
        e.preventDefault();
        dropZone.classList.add('dragover');
    });

    dropZone.addEventListener('dragleave', function () {
        dropZone.classList.remove('dragover');
    });

    dropZone.addEventListener('drop', function (e) {
        e.preventDefault();
        dropZone.classList.remove('dragover');
        handleFiles(e.dataTransfer.files);
    });

    // File input change
    fileInput.addEventListener('change', function () {
        handleFiles(fileInput.files);
        fileInput.value = '';
    });

    function handleFiles(fileList) {
        for (var i = 0; i < fileList.length; i++) {
            if (fileList[i].type.startsWith('image/')) {
                selectedFiles.push(fileList[i]);
            }
        }
        if (selectedFiles.length > 0) {
            renderPreviews();
            uploadAll();
        }
    }

    function clearPreviews() {
        // Revoke object URLs before clearing to prevent memory leaks
        var images = previewArea.querySelectorAll('img');
        images.forEach(function (img) {
            if (img.src.startsWith('blob:')) URL.revokeObjectURL(img.src);
        });
        while (previewArea.firstChild) {
            previewArea.removeChild(previewArea.firstChild);
        }
    }

    function renderPreviews() {
        clearPreviews();
        previewArea.classList.remove('hidden');

        selectedFiles.forEach(function (file, idx) {
            var item = document.createElement('div');
            item.className = 'preview-item';
            item.id = 'preview-' + idx;

            var img = document.createElement('img');
            var objectUrl = URL.createObjectURL(file);
            img.src = objectUrl;
            item.appendChild(img);

            var removeBtn = document.createElement('button');
            removeBtn.className = 'remove-btn';
            removeBtn.textContent = '\u00d7';
            removeBtn.addEventListener('click', function () {
                selectedFiles.splice(idx, 1);
                renderPreviews();
            });
            item.appendChild(removeBtn);

            previewArea.appendChild(item);
        });
    }

    // Client-side image resize
    function resizeImage(file) {
        return new Promise(function (resolve) {
            var img = new Image();
            var objectUrl = URL.createObjectURL(file);
            img.onload = function () {
                URL.revokeObjectURL(objectUrl);
                var w = img.width;
                var h = img.height;

                if (w <= MAX_SIZE && h <= MAX_SIZE) {
                    resolve(file);
                    return;
                }

                var ratio = Math.min(MAX_SIZE / w, MAX_SIZE / h);
                var newW = Math.round(w * ratio);
                var newH = Math.round(h * ratio);

                var canvas = document.createElement('canvas');
                canvas.width = newW;
                canvas.height = newH;
                var ctx = canvas.getContext('2d');
                ctx.drawImage(img, 0, 0, newW, newH);

                canvas.toBlob(function (blob) {
                    var resized = new File([blob], file.name, {
                        type: 'image/jpeg',
                        lastModified: Date.now()
                    });
                    resolve(resized);
                }, 'image/jpeg', 0.85);
            };
            img.src = objectUrl;
        });
    }

    async function uploadAll() {
        var name = uploaderName.value.trim();
        if (name) {
            localStorage.setItem('ellabm_name', name);
        }

        progressArea.classList.remove('hidden');
        uploadStatus.classList.add('hidden');

        var total = selectedFiles.length;
        var completed = 0;
        var failed = 0;

        // Copy array so we can clear selectedFiles
        var filesToUpload = selectedFiles.slice();
        selectedFiles = [];

        for (var i = 0; i < filesToUpload.length; i++) {
            var previewItem = document.getElementById('preview-' + i);
            if (previewItem) previewItem.classList.add('uploading');

            progressText.textContent = 'Uploader ' + (i + 1) + ' af ' + total + '...';
            progressFill.style.width = ((i / total) * 100) + '%';

            try {
                var resized = await resizeImage(filesToUpload[i]);
                var formData = new FormData();
                formData.append('photo', resized);
                if (name) formData.append('uploaderName', name);

                var response = await fetch(API_UPLOAD, {
                    method: 'POST',
                    headers: { 'X-Ellabm-Password': password },
                    body: formData
                });

                if (response.ok) {
                    completed++;
                    if (previewItem) {
                        previewItem.classList.remove('uploading');
                        previewItem.classList.add('done');
                    }
                } else {
                    if (response.status === 401) {
                        localStorage.removeItem(STORAGE_KEY);
                        location.reload();
                        return;
                    }
                    failed++;
                    if (previewItem) {
                        previewItem.classList.remove('uploading');
                        previewItem.classList.add('failed');
                    }
                }
            } catch (err) {
                failed++;
                if (previewItem) {
                    previewItem.classList.remove('uploading');
                    previewItem.classList.add('failed');
                }
            }
        }

        progressFill.style.width = '100%';

        uploadStatus.classList.remove('hidden', 'success', 'error');
        if (failed === 0) {
            uploadStatus.className = 'success';
            uploadStatus.textContent = completed + ' billede' + (completed > 1 ? 'r' : '') + ' uploadet!';
        } else {
            uploadStatus.className = 'error';
            uploadStatus.textContent = completed + ' uploadet, ' + failed + ' fejlede.';
        }

        // Clear previews after a delay
        setTimeout(function () {
            clearPreviews();
            previewArea.classList.add('hidden');
            progressArea.classList.add('hidden');
            progressFill.style.width = '0%';
        }, 3000);
    }

    init();
})();
