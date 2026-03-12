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

    var photos = [];
    var shuffled = [];
    var currentIndex = -1;
    var slideTimer = null;

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
                    // If slideshow wasn't running, start it
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

        // Preload next image
        slideNext.src = photo.url;
        slideNext.onload = function () {
            // Fade transition
            slideCurrent.classList.remove('active');
            slideNext.classList.add('active');

            // Show name briefly
            photoName.textContent = photo.name;
            photoInfo.classList.add('visible');
            setTimeout(function () {
                photoInfo.classList.remove('visible');
            }, 3000);

            // Swap references for next transition
            var tmp = slideCurrent;
            slideCurrent = slideNext;
            slideNext = tmp;
        };

        // If image fails to load, skip to next
        slideNext.onerror = function () {
            setTimeout(nextSlide, 500);
        };
    }

    function startSlideshow() {
        if (slideTimer) clearInterval(slideTimer);
        slideTimer = setInterval(nextSlide, slideDuration);
    }

    // Initial fetch
    fetchPhotos();

    // Poll for new photos
    setInterval(fetchPhotos, POLL_INTERVAL);
})();
