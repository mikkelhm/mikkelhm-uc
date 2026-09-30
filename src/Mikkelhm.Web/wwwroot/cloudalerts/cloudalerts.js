(function () {
  'use strict';

  var form = document.getElementById('ca-filters');
  if (form) {
    form.addEventListener('change', function () {
      Array.prototype.forEach.call(form.elements, function (el) {
        if (!el.name) return;
        var isDefaultTests = el.name === 'tests' && el.value === 'show';
        if ((el.value === '' && el.type !== 'radio') || isDefaultTests) el.disabled = true;
      });
      form.submit();
    });
  }

  function pad(n) { return n < 10 ? '0' + n : String(n); }

  function formatLocal(d) {
    return pad(d.getDate()) + '-' + pad(d.getMonth() + 1) + '-' + d.getFullYear() + ' ' + pad(d.getHours()) + ':' + pad(d.getMinutes());
  }

  var rtf = window.Intl && Intl.RelativeTimeFormat ? new Intl.RelativeTimeFormat('en', { numeric: 'auto' }) : null;

  function relative(d) {
    if (!rtf) return '';
    var seconds = Math.round((d.getTime() - Date.now()) / 1000);
    var units = [['day', 86400], ['hour', 3600], ['minute', 60], ['second', 1]];
    for (var i = 0; i < units.length; i++) {
      if (Math.abs(seconds) >= units[i][1] || units[i][0] === 'second') {
        return rtf.format(Math.round(seconds / units[i][1]), units[i][0]);
      }
    }
    return '';
  }

  document.querySelectorAll('time[data-local]').forEach(function (t) {
    var d = new Date(t.getAttribute('datetime'));
    if (isNaN(d.getTime())) return;
    t.textContent = formatLocal(d);
    t.title = t.getAttribute('datetime');
    var rel = t.parentElement.querySelector('[data-rel]');
    if (rel) rel.textContent = relative(d);
  });
})();
