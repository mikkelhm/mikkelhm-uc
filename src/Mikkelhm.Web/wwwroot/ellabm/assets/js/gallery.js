(function () {
    'use strict';

    var API_PHOTOS = '/umbraco/api/ellabm/photos';
    var POLL_INTERVAL = 30000;
    var config = window.ELLABM_CONFIG || {};
    var slideDuration = (config.slideDuration || 8) * 1000;

    var slideCurrent = document.getElementById('slide-current');
    var slideNext = document.getElementById('slide-next');
    var photoInfo = document.getElementById('photo-info');
    var photoName = document.getElementById('photo-name');
    var noPhotos = document.getElementById('no-photos');
    var modeToggle = document.getElementById('mode-toggle');
    var polaroidFrame = document.getElementById('polaroid-frame');
    var polaroidImg = document.getElementById('polaroid-img');
    var polaroidCaption = document.getElementById('polaroid-caption');
    var polaroidInner = document.getElementById('polaroid-inner');

    var photos = [];
    var shuffled = [];
    var currentIndex = -1;
    var slideTimer = null;
    var storedMode = localStorage.getItem('ellabm_gallery_mode');
    var isPolaroid = storedMode === null ? true : storedMode === 'polaroid';

    var tiltClasses = ['tilt-right', 'tilt-left', 'tilt-none'];

    function shuffle(arr) {
        var a = arr.slice();
        for (var i = a.length - 1; i > 0; i--) {
            var j = Math.floor(Math.random() * (i + 1));
            var tmp = a[i];
            a[i] = a[j];
            a[j] = tmp;
        }
        return a;
    }

    function applyMode() {
        if (isPolaroid) {
            document.body.classList.add('polaroid-mode');
            polaroidFrame.classList.remove('hidden');
            modeToggle.textContent = '🖥️';
            modeToggle.title = 'Skift til fuldskærm';
        } else {
            document.body.classList.remove('polaroid-mode');
            polaroidFrame.classList.add('hidden');
            modeToggle.textContent = '🖼️';
            modeToggle.title = 'Skift til polaroid';
        }
    }

    function applyRandomTilt() {
        tiltClasses.forEach(function (c) { polaroidInner.classList.remove(c); });
        var cls = tiltClasses[Math.floor(Math.random() * tiltClasses.length)];
        polaroidInner.classList.add(cls);
    }

    modeToggle.addEventListener('click', function () {
        isPolaroid = !isPolaroid;
        localStorage.setItem('ellabm_gallery_mode', isPolaroid ? 'polaroid' : 'fullscreen');
        applyMode();

        // Re-show current photo in new mode
        if (shuffled.length > 0 && currentIndex >= 0) {
            var photo = shuffled[currentIndex];
            if (isPolaroid) {
                polaroidImg.src = photo.url;
                polaroidCaption.textContent = photo.name;
                applyRandomTilt();
            }
        }
    });

    // Show toggle on mouse movement, hide after inactivity
    var hideTimer;
    document.addEventListener('mousemove', function () {
        modeToggle.style.opacity = '0.7';
        clearTimeout(hideTimer);
        hideTimer = setTimeout(function () {
            modeToggle.style.opacity = '0';
        }, 3000);
    });
    // Also show on touch
    document.addEventListener('touchstart', function () {
        modeToggle.style.opacity = '0.7';
        clearTimeout(hideTimer);
        hideTimer = setTimeout(function () {
            modeToggle.style.opacity = '0';
        }, 4000);
    });

    function fetchPhotos() {
        fetch(API_PHOTOS)
            .then(function (res) { return res.json(); })
            .then(function (data) {
                if (!data || data.length === 0) return;

                var newCount = data.length;
                var oldCount = photos.length;
                photos = data;

                if (newCount !== oldCount) {
                    shuffled = shuffle(photos);
                    if (oldCount === 0) {
                        noPhotos.classList.add('hidden');
                        currentIndex = -1;
                        nextSlide();
                        startSlideshow();
                    }
                }
            })
            .catch(function () { /* silently retry next poll */ });
    }

    function nextSlide() {
        currentIndex++;
        if (currentIndex >= shuffled.length) {
            shuffled = shuffle(photos);
            currentIndex = 0;
        }

        var photo = shuffled[currentIndex];
        if (!photo) return;

        if (isPolaroid) {
            showPolaroid(photo);
        } else {
            showFullscreen(photo);
        }
    }

    function showFullscreen(photo) {
        slideNext.src = photo.url;
        slideNext.onload = function () {
            slideCurrent.classList.remove('active');
            slideNext.classList.add('active');

            photoName.textContent = photo.name;
            photoInfo.classList.add('visible');
            setTimeout(function () {
                photoInfo.classList.remove('visible');
            }, 3000);

            var tmp = slideCurrent;
            slideCurrent = slideNext;
            slideNext = tmp;
        };

        slideNext.onerror = function () {
            setTimeout(nextSlide, 500);
        };
    }

    function showPolaroid(photo) {
        // Preload the image
        var preload = new Image();
        preload.onload = function () {
            // Fade out
            polaroidFrame.classList.add('fading');

            setTimeout(function () {
                polaroidImg.src = photo.url;
                polaroidCaption.textContent = photo.name;
                applyRandomTilt();

                // Fade back in
                polaroidFrame.classList.remove('fading');
            }, 400);
        };

        preload.onerror = function () {
            setTimeout(nextSlide, 500);
        };

        preload.src = photo.url;
    }

    function startSlideshow() {
        if (slideTimer) clearInterval(slideTimer);
        slideTimer = setInterval(nextSlide, slideDuration);
    }

    // Initialize mode
    applyMode();

    // Initial fetch
    fetchPhotos();

    // Poll for new photos
    setInterval(fetchPhotos, POLL_INTERVAL);
})();
