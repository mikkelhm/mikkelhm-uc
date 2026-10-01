(function () {
  'use strict';

  // Builds the filter query string from form controls, leaving out empty values and the
  // default tests=show. The controls themselves are never touched, so Back navigation
  // (bfcache) restores a fully usable form.
  function queryFrom(fields) {
    var params = new URLSearchParams();
    Array.prototype.forEach.call(fields, function (el) {
      if (!el.name || el.value === '') return;
      if (el.type === 'radio' && !el.checked) return;
      if (el.type === 'submit' || el.type === 'button') return;
      if (el.name === 'tests' && el.value === 'show') return;
      params.append(el.name, el.value);
    });
    var query = params.toString();
    return query ? '?' + query : '';
  }

  if (typeof module !== 'undefined' && module.exports) {
    module.exports = { queryFrom: queryFrom };
  }

  if (typeof document === 'undefined') return;

  var form = document.getElementById('ca-filters');
  if (form) {
    form.addEventListener('change', function () {
      window.location.assign(form.getAttribute('action') + queryFrom(form.elements));
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
