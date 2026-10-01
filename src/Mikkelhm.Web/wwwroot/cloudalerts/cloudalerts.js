(function () {
  'use strict';

  // Builds the filter query string from form controls, leaving out empty values, unchecked
  // radios/checkboxes and the default tests=show. The controls themselves are never touched,
  // so Back navigation (bfcache) restores a fully usable form.
  function queryFrom(fields) {
    var params = new URLSearchParams();
    Array.prototype.forEach.call(fields, function (el) {
      if (!el.name || el.value === '') return;
      if ((el.type === 'radio' || el.type === 'checkbox') && !el.checked) return;
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
})();
