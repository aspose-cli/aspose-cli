(function () {
  'use strict';

  var csrfMeta = document.querySelector('meta[name="aspose-csrf"]');
  var csrf = csrfMeta ? csrfMeta.content : '';
  var status = null;
  var polling = null;
  var statusRequest = null;
  var statusEpoch = 0;
  var preferenceDrafts = new Map();
  var rememberPreference = null;
  var preferencesSave = null;
  var licenseTarget = null;
  var $ = function (id) { return document.getElementById(id); };
  var THEME_KEY = 'aspose-cli-theme';

  function readTheme() {
    try {
      var saved = window.localStorage.getItem(THEME_KEY);
      return saved === 'light' || saved === 'dark' ? saved : 'system';
    } catch (error) {
      return 'system';
    }
  }

  /**
   * Applies the theme here and leaves it where the viewer finds it. The App
   * and the documents it frames are one origin, so the framed page picks the
   * change up from storage without either side addressing the other.
   */
  function applyTheme(theme) {
    if (theme === 'system') {
      document.documentElement.removeAttribute('data-theme');
    } else {
      document.documentElement.setAttribute('data-theme', theme);
    }
    var toggle = $('theme-toggle');
    if (toggle) {
      toggle.setAttribute('aria-pressed', String(theme !== 'system'));
      toggle.title = 'Theme: ' + theme;
    }
  }

  function cycleTheme() {
    var next = readTheme() === 'system' ? 'dark' : readTheme() === 'dark' ? 'light' : 'system';
    try {
      if (next === 'system') {
        window.localStorage.removeItem(THEME_KEY);
      } else {
        window.localStorage.setItem(THEME_KEY, next);
      }
    } catch (error) {
      // Private browsing: the choice lasts for this page only.
    }
    applyTheme(next);
  }

  function node(tag, className, text) {
    var result = document.createElement(tag);
    if (className) {
      result.className = className;
    }
    if (text !== undefined) {
      result.textContent = text;
    }
    return result;
  }

  function routeName() {
    if (location.pathname === '/preview') {
      return 'preview';
    }
    if (location.pathname === '/settings') {
      return 'settings';
    }
    return location.pathname === '/home' ? 'home' : 'welcome';
  }

  function go(path) {
    if (location.pathname !== path) {
      history.pushState({}, '', path);
    }
    window.scrollTo(0, 0);
    renderRoute(true);
    loadStatus();
  }

  function announce(message) {
    $('workspace-live').textContent = '';
    window.setTimeout(function () {
      $('workspace-live').textContent = message;
    }, 20);
  }

  function setActivity(message) {
    $('live-chip').lastChild.textContent = message;
    announce(message);
  }

  function toast(message, isError) {
    var element = $('toast');
    element.textContent = message;
    element.className = 'toast' + (isError ? ' error' : '');
    element.hidden = false;
    announce(message);
    window.clearTimeout(element._timer);
    element._timer = window.setTimeout(function () {
      element.hidden = true;
    }, 5000);
  }

  async function api(path, options) {
    options = options || {};
    options.headers = options.headers || {};
    if (options.method && options.method !== 'GET') {
      options.headers['X-CSRF-Token'] = csrf;
    }

    var response = await fetch(path, options);
    var data = await response.json().catch(function () {
      return {
        ok: false,
        message: 'The local App returned an unreadable response.'
      };
    });
    if (!response.ok || data.ok === false) {
      throw new Error(data.message || ('Request failed (' + response.status + ')'));
    }
    if (options.method && options.method !== 'GET') { statusEpoch++; }
    return data;
  }

  function renderRoute(moveFocus) {
    var route = routeName();
    var visible = route === 'welcome' ? 'home' : route;
    document.body.classList.toggle('preview-active', visible === 'preview');
    ['home', 'preview', 'settings'].forEach(function (name) {
      $(name + '-view').hidden = name !== visible;
    });
    $('getting-started').hidden = route !== 'welcome';
    document.querySelectorAll('[data-route]').forEach(function (element) {
      var target = element.getAttribute('data-route');
      var activePath = route === 'welcome' ? '/home' : location.pathname;
      element.classList.toggle('active', target === activePath);
      if (element.tagName === 'A') {
        if (target === activePath) {
          element.setAttribute('aria-current', 'page');
        } else {
          element.removeAttribute('aria-current');
        }
      }
    });
    if (polling) {
      window.clearInterval(polling);
    }
    polling = window.setInterval(loadStatus, visible === 'preview' ? 2500 : 15000);
    if (moveFocus) {
      window.requestAnimationFrame(function () {
        var heading = Array.prototype.find.call($(visible + '-view').querySelectorAll('h1'), function (candidate) {
          return candidate.offsetParent !== null;
        });
        if (heading) {
          heading.focus({ preventScroll: true });
        }
      });
    }
  }

  function loadStatus() {
    if (!statusRequest) {
      var request = { epoch: statusEpoch, promise: null };
      statusRequest = request;
      request.promise = readStatus(request.epoch).finally(function () {
        if (statusRequest === request) { statusRequest = null; }
      });
    }
    var pending = statusRequest;
    return pending.promise.then(function () {
      if (pending.epoch !== statusEpoch) { return loadStatus(); }
    });
  }

  async function readStatus(epoch) {
    try {
      var next = await api('/api/status', { method: 'GET' });
      if (epoch === statusEpoch) {
        status = next;
        renderStatus();
      }
    } catch (error) {
      if (epoch === statusEpoch) {
        setActivity('Disconnected');
        toast(error.message, true);
        if (!status) {
          $('preview-loading-detail').textContent = 'The workspace did not answer; it retries shortly.';
        }
      }
    }
  }

  function currentProduct() {
    return (status.products || []).find(function (product) {
      return product.id === status.product;
    }) || status.products[0];
  }

  function currentProductLicense() {
    return (status.license.products || []).find(function (product) {
      return product.product === status.product;
    });
  }

  function commercialState() {
    var applicable = (status.license.products || []).filter(function (product) {
      return product.applicable;
    });
    var licensed = applicable.filter(function (product) {
      return product.mode === 'licensed';
    }).length;
    var broken = applicable.filter(function (product) {
      return Boolean(product.problem);
    }).length;
    return {
      products: applicable,
      licensed: licensed,
      broken: broken,
      complete: applicable.length > 0 && licensed === applicable.length
    };
  }

  function renderStatus() {
    if (!status) {
      return;
    }

    document.body.dataset.experience = status.experience;
    $('build-chip').textContent = status.displayName;
    setActivity(status.file ? 'Preview live' : 'Ready');
    $('file-input').accept = (status.supportedExtensions || []).join(',');
    syncPreferences();
    renderLicenseComposition();
    renderProducts();
    renderRecents();
    renderPreviewOptions();
    renderAgentCommands();
    renderDiagnostics();
    renderPreview();
  }

  function renderLicenseComposition() {
    var bannerMount = $('status-banner-mount');
    var settingsMount = $('status-settings-mount');
    var previewMount = $('preview-notice-mount');
    bannerMount.replaceChildren();
    settingsMount.replaceChildren();
    previewMount.replaceChildren();
    if (status.experience !== 'licensed') {
      return;
    }

    var state = commercialState();
    var banner = node('aside', 'license-banner');
    banner.dataset.ui = 'license-banner';
    if (state.broken) {
      banner.classList.add('error');
    } else if (!state.complete) {
      banner.classList.add('warn');
    }
    var copy = node('div');
    copy.append(
      node('span', 'eyebrow', 'LICENSES'),
      node('strong', '', state.broken
        ? 'License configuration needs attention.'
        : state.complete
          ? 'All compiled products are licensed.'
          : 'Evaluation mode is active.'),
      node('p', '', state.complete
        ? 'Opening a file remains the primary workspace action.'
        : 'You can open files now. Output will continue to disclose evaluation effects.'));
    var manage = node('button', 'button secondary compact', 'Manage licenses');
    manage.type = 'button';
    manage.dataset.route = '/settings';
    banner.append(copy, manage);
    bannerMount.append(banner);

    settingsMount.append(createLicenseSettings(state));

    var productLicense = currentProductLicense();
    if (status.file && productLicense && productLicense.applicable
        && productLicense.mode !== 'licensed') {
      var notice = node('div', 'preview-notice');
      notice.dataset.ui = 'evaluation-notice';
      notice.append(
        node('span', '', productLicense.name + ' is running in evaluation mode; preview or output may contain disclosed evaluation effects.'),
        routeButton('Review settings', '/settings', 'button secondary compact'));
      previewMount.append(notice);
    }
  }

  function createLicenseSettings(state) {
    var card = node('article', 'settings-card wide');
    card.dataset.ui = 'license-settings';
    var heading = node('div', 'card-heading');
    var copy = node('div');
    copy.append(
      node('h2', '', 'Product licenses'),
      node('p', '', state.broken
        ? state.broken + ' product configuration' + (state.broken === 1 ? '' : 's') + ' need attention.'
        : state.complete
          ? 'Every compiled product is licensed.'
          : state.licensed + ' of ' + state.products.length + ' products licensed.'));
    var action = node('button', 'button primary compact', 'Choose license file');
    action.type = 'button';
    action.dataset.licenseInstall = '';
    heading.append(copy, action);
    var list = node('div', 'license-list');
    list.id = 'license-list';
    status.license.products.forEach(function (product) {
      list.append(createLicenseCard(product));
    });
    if (status.license.sharedUserLicenseInstalled) {
      list.append(createSharedLicenseCard());
    }
    var input = node('input');
    input.id = 'license-input';
    input.type = 'file';
    input.accept = '.lic';
    input.hidden = true;
    input.addEventListener('change', function () {
      uploadLicense(this.files[0]);
    });
    card.append(heading, list, input);
    return card;
  }

  function createLicenseCard(product) {
    var card = node('section', 'license-product');
    var header = node('div', 'license-product-header');
    var licensed = product.mode === 'licensed';
    var broken = Boolean(product.problem);
    var badge = node(
      'span',
      'chip ' + (!product.applicable || licensed ? '' : broken ? 'error' : 'warn'),
      !product.applicable
        ? 'Not applicable'
        : licensed ? 'Licensed' : broken ? 'Needs attention' : 'Evaluation');
    header.append(node('h3', '', product.name), badge);
    card.append(header);

    if (product.applicable) {
      var details = node('dl', 'details');
      appendDetail(details, 'Source', product.source || 'Not configured');
      appendDetail(details, 'Stored at', product.path || 'None');
      card.append(details);
    }
    if (broken) {
      card.append(node(
        'div',
        'inline-alert',
        product.problem + (product.hint ? ' ' + product.hint : '')));
    }
    if (product.applicable) {
      var actions = node('div', 'button-row');
      var install = node(
        'button',
        'button secondary compact',
        product.userLicenseInstalled ? 'Replace saved license' : 'Install for ' + product.name);
      install.type = 'button';
      install.dataset.licenseInstall = product.product;
      actions.append(install);
      if (product.userLicenseInstalled) {
        var remove = node('button', 'button danger compact', 'Remove saved license');
        remove.type = 'button';
        remove.dataset.licenseRemove = product.product;
        remove.dataset.licenseName = product.name;
        actions.append(remove);
      }
      card.append(actions);
    }
    return card;
  }

  function createSharedLicenseCard() {
    var card = node('section', 'license-product');
    var header = node('div', 'license-product-header');
    header.append(
      node('h3', '', 'Shared Aspose.Total license'),
      node('span', 'chip muted', 'Shared'));
    var actions = node('div', 'button-row');
    var remove = node('button', 'button danger compact', 'Remove shared license');
    remove.type = 'button';
    remove.dataset.licenseRemove = 'shared';
    remove.dataset.licenseName = 'shared';
    actions.append(remove);
    card.append(header, actions);
    return card;
  }

  function routeButton(text, route, className) {
    var button = node('button', className, text);
    button.type = 'button';
    button.dataset.route = route;
    return button;
  }

  function appendDetail(root, label, value) {
    var row = node('div');
    row.append(node('dt', '', label), node('dd', '', value));
    root.append(row);
  }

  function renderProducts() {
    var root = $('product-grid');
    root.replaceChildren();
    var products = status.products || [];
    $('product-count').textContent = products.length + ' compiled products';
    products.forEach(function (product) {
      var card = node('article', 'product-card');
      card.dataset.product = product.id;
      var head = node('div', 'product-card-head');
      var title = node('div');
      title.append(
        node('h3', '', product.name),
        node('p', '', product.preview.fidelity === 'rendered'
          ? 'Rendered preview is available for visual inspection.'
          : 'Semantic preview; confirm final layout in a compatible viewer.'));
      head.append(
        title,
        node('span', 'chip ' + (product.preview.fidelity === 'rendered' ? '' : 'muted'), product.preview.fidelity));
      card.append(head);

      var extensions = [];
      (product.formats || []).forEach(function (format) {
        (format.extensions || []).forEach(function (extension) {
          if (extensions.indexOf(extension) < 0) {
            extensions.push(extension);
          }
        });
      });
      var tags = node('div', 'tag-list');
      extensions.slice(0, 8).forEach(function (extension) {
        tags.append(node('span', 'tag', extension));
      });
      if (extensions.length > 8) {
        tags.append(node('span', 'tag', '+' + (extensions.length - 8)));
      }
      card.append(tags);
      card.append(node(
        'p',
        '',
        'Commands: ' + product.verbs.join(', ') + '. Review view: ' + product.review.defaultView + '.'));
      var skill = (status.skills || []).find(function (candidate) {
        return candidate.product === product.id;
      });
      if (skill) {
        card.append(node(
          'div',
          'skill-line',
          'aspose-cli skill install ' + skill.name + ' --host codex --scope project'));
      }
      root.append(card);
    });
  }

  function renderRecents() {
    var root = $('recent-list');
    root.replaceChildren();
    if (!status.recentFiles.length) {
      root.append(node('div', 'empty-list', 'No recent files yet. Open a file to begin.'));
      $('clear-recents').hidden = true;
      return;
    }
    $('clear-recents').hidden = false;
    status.recentFiles.forEach(function (file) {
      var row = node('div', 'recent-item');
      var open = node('button', 'recent-open');
      open.type = 'button';
      open.append(
        node('strong', '', file.name),
        node('small', '', file.productName
          ? file.productName + (file.view ? ' / ' + file.view : '')
          : 'Local file'));
      open.addEventListener('click', function () { openRecent(file.id); });
      var remove = node('button', 'recent-remove', 'Remove');
      remove.type = 'button';
      remove.setAttribute('aria-label', 'Remove ' + file.name + ' from recent files');
      remove.addEventListener('click', function () { removeRecent(file.id); });
      row.append(open, remove);
      root.append(row);
    });
  }

  function mergePreference(field, value, saving) {
    if (!field) { return { saved: value, value: value }; }
    if (!saving) {
      if (field.value === field.saved) { field.value = value; }
      field.saved = value;
    }
    return field;
  }

  function syncPreferences() {
    var product = status.product;
    preferenceDrafts.set(product, mergePreference(preferenceDrafts.get(product),
      status.defaultView, preferencesSave && preferencesSave.product === product));
    rememberPreference = mergePreference(rememberPreference,
      status.rememberRecentFiles, Boolean(preferencesSave));
  }

  function renderPreviewOptions() {
    var draft = preferenceDrafts.get(status.product);
    if (!draft || !rememberPreference) { return; }
    var select = $('default-view');
    if (select.dataset.product !== status.product) {
      select.replaceChildren();
      (status.previewViews || []).forEach(function (view) {
        var option = node('option', '', view.displayName);
        option.value = view.id;
        select.append(option);
      });
      select.dataset.product = status.product;
    }
    select.value = draft.value;
    $('remember-recents').checked = rememberPreference.value;
    var button = $('save-preferences');
    button.disabled = Boolean(preferencesSave);
    button.textContent = preferencesSave ? 'Saving preferences…' : 'Save preferences';
    var feedback = $('preferences-feedback');
    feedback.textContent = draft.message || '';
    feedback.hidden = !draft.message;
    feedback.classList.toggle('warning', Boolean(draft.warning));
  }

  function renderAgentCommands() {
    var root = $('agent-commands');
    root.replaceChildren();
    var skills = status.skills || [];
    if (!skills.length) {
      root.append(node('span', '', 'This build has no bundled Agent Skills.'));
      return;
    }
    skills.forEach(function (skill) {
      var product = skill.product && (status.products || []).find(function (candidate) {
        return candidate.id === skill.product;
      });
      var command = node('div', 'agent-command');
      command.append(
        node('strong', '', skill.product ? (product ? product.name : skill.product) : 'Platform'),
        node('span', '', 'aspose-cli skill install ' + skill.name + ' --host codex --scope project'));
      root.append(command);
    });
  }

  function renderDiagnostics() {
    var root = $('diagnostics');
    root.replaceChildren();
    status.diagnostics.forEach(function (item) {
      var card = node('div', 'diagnostic ' + item.status);
      card.append(
        node('strong', '', item.name),
        node('span', '', item.detail + (item.hint ? ' - ' + item.hint : '')));
      root.append(card);
    });
  }

  /**
   * One tab per open document. Switching tabs asks the App which document is
   * on screen; the viewer service keeps every one of them rendering, so a tab
   * comes back to exactly what it was showing.
   */
  function renderTabs() {
    var strip = $('document-tabs');
    var documents = status.documents || [];
    strip.replaceChildren();
    strip.hidden = documents.length < 2;
    documents.forEach(function (document_) {
      var tab = node('button', 'document-tab' + (document_.active ? ' active' : ''));
      tab.type = 'button';
      tab.setAttribute('role', 'tab');
      tab.setAttribute('aria-selected', String(document_.active));
      tab.title = document_.fileName + ' (' + document_.view + ')';
      tab.append(node('span', 'tab-name', document_.fileName));
      if (document_.uploadedCopy) {
        tab.append(node('span', 'tab-badge', 'copy'));
      }
      tab.addEventListener('click', function () { activateDocument(document_.id); });
      var close = node('span', 'tab-close', '\u00d7');
      close.setAttribute('role', 'button');
      close.setAttribute('aria-label', 'Close ' + document_.fileName);
      close.addEventListener('click', function (event) {
        event.stopPropagation();
        closeDocument(document_.id);
      });
      tab.append(close);
      strip.append(tab);
    });
  }

  /** The views the active document's product offers, and the one on screen. */
  function renderViewChoice() {
    var field = $('document-view-field');
    var select = $('document-view');
    var product = currentProduct();
    var views = product && product.preview ? product.preview.views : [];
    var active = (status.documents || []).find(function (item) { return item.active; });
    if (!active || views.length < 2) {
      field.hidden = true;
      return;
    }
    field.hidden = false;
    select.replaceChildren();
    views.forEach(function (view) {
      var option = node('option', '', view.displayName);
      option.value = view.id;
      option.selected = view.id === active.view;
      select.append(option);
    });
    select.onchange = function () { showDocument(active.id, select.value); };
  }

  async function activateDocument(id) {
    try {
      await api('/api/documents/activate', { method: 'POST', body: JSON.stringify({ id: id }) });
      await loadStatus();
    } catch (error) {
      toast(error.message, true);
    }
  }

  async function closeDocument(id) {
    try {
      await api('/api/documents/close', { method: 'POST', body: JSON.stringify({ id: id }) });
      await loadStatus();
      if (!status.previewUrl) {
        go('/home');
      }
    } catch (error) {
      toast(error.message, true);
    }
  }

  async function showDocument(id, view) {
    try {
      setActivity('Rendering ' + view);
      await api('/api/documents/view', { method: 'POST', body: JSON.stringify({ id: id, view: view }) });
      await loadStatus();
      setActivity('Ready');
    } catch (error) {
      toast(error.message, true);
    }
  }

  function renderPreview() {
    var product = currentProduct();
    renderTabs();
    renderViewChoice();
    $('preview-file').textContent = status.file || 'No file open';
    $('preview-kind').textContent = status.uploadedCopy
      ? 'Temporary local copy'
      : product
        ? product.name + ' / ' + product.preview.fidelity + ' preview'
        : 'Local preview';
    var frame = $('preview-frame');
    var empty = $('preview-empty');
    // Until the first status arrives the page cannot know whether a file is open.
    $('preview-loading').hidden = true;
    if (status.previewUrl) {
      empty.hidden = true;
      frame.hidden = false;
      if (frame.src !== status.previewUrl) {
        frame.src = status.previewUrl;
      }
    } else {
      frame.hidden = true;
      frame.removeAttribute('src');
      empty.hidden = false;
    }
  }

  async function chooseNative() {
    try {
      setActivity('Choosing file');
      var result = await api('/api/files/pick', { method: 'POST' });
      if (result.fallbackUpload) {
        $('file-input').click();
        setActivity('Ready');
        return;
      }
      if (result.message) {
        toast(result.message);
      }
      await loadStatus();
      if (status.previewUrl) {
        go('/preview');
      }
    } catch (error) {
      toast(error.message, true);
    }
  }

  async function uploadFile(file) {
    if (!file) {
      return;
    }
    try {
      setActivity('Opening ' + file.name);
      await api('/api/files/upload', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/octet-stream',
          'X-File-Name': encodeURIComponent(file.name)
        },
        body: file
      });
      await loadStatus();
      go('/preview');
    } catch (error) {
      toast(error.message, true);
    }
  }

  function chooseLicense(productId) {
    var input = $('license-input');
    if (!input) {
      return;
    }
    licenseTarget = productId || null;
    input.click();
  }

  async function uploadLicense(file) {
    if (!file) {
      return;
    }
    var target = licenseTarget;
    try {
      setActivity(target ? 'Validating product license' : 'Detecting license');
      var headers = {
        'Content-Type': 'application/octet-stream',
        'X-File-Name': encodeURIComponent(file.name)
      };
      if (target) {
        headers['X-Product'] = target;
      }
      var result = await api('/api/license', {
        method: 'POST',
        headers: headers,
        body: file
      });
      toast('License saved. Re-rendering the open document.');
      await loadStatus();
    } catch (error) {
      toast(error.message, true);
    } finally {
      licenseTarget = null;
    }
  }

  async function removeLicense(productId, name) {
    if (!window.confirm('Remove the saved ' + name + ' license?')) {
      return;
    }
    try {
      var result = await api('/api/license', {
        method: 'DELETE',
        headers: { 'X-Product': productId }
      });
      toast('Saved license removed. Re-rendering the open document.');
      await loadStatus();
    } catch (error) {
      toast(error.message, true);
    }
  }

  async function openRecent(id) {
    try {
      await api('/api/recent/open', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: id })
      });
      await loadStatus();
      go('/preview');
    } catch (error) {
      toast(error.message, true);
    }
  }

  async function removeRecent(id) {
    try {
      await api('/api/recent/remove', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ id: id })
      });
      await loadStatus();
    } catch (error) {
      toast(error.message, true);
    }
  }

  document.addEventListener('click', function (event) {
    var route = event.target.closest('[data-route]');
    if (route) {
      event.preventDefault();
      go(route.getAttribute('data-route'));
      return;
    }
    var dismiss = event.target.closest('[data-dismiss-start]');
    if (dismiss) {
      go('/home');
      return;
    }
    var install = event.target.closest('[data-license-install]');
    if (install) {
      chooseLicense(install.dataset.licenseInstall);
      return;
    }
    var remove = event.target.closest('[data-license-remove]');
    if (remove) {
      removeLicense(remove.dataset.licenseRemove, remove.dataset.licenseName);
    }
  });

  window.addEventListener('popstate', function () {
    renderRoute(true);
    loadStatus();
  });

  $('open-file').addEventListener('click', chooseNative);
  $('preview-open').addEventListener('click', chooseNative);
  $('theme-toggle').addEventListener('click', cycleTheme);
  $('file-input').addEventListener('change', function () {
    uploadFile(this.files[0]);
    this.value = '';
  });

  var drop = $('drop-zone');
  ['dragenter', 'dragover'].forEach(function (name) {
    drop.addEventListener(name, function (event) {
      event.preventDefault();
      drop.classList.add('dragging');
    });
  });
  ['dragleave', 'drop'].forEach(function (name) {
    drop.addEventListener(name, function (event) {
      event.preventDefault();
      drop.classList.remove('dragging');
    });
  });
  drop.addEventListener('drop', function (event) {
    uploadFile(event.dataTransfer.files[0]);
  });
  drop.addEventListener('click', function () {
    $('file-input').click();
  });

  $('clear-recents').addEventListener('click', async function () {
    try {
      await api('/api/recent/clear', { method: 'POST' });
      await loadStatus();
    } catch (error) {
      toast(error.message, true);
    }
  });

  $('default-view').addEventListener('change', function () {
    var draft = preferenceDrafts.get(this.dataset.product);
    if (draft) { draft.value = this.value; }
  });

  $('remember-recents').addEventListener('change', function () {
    if (rememberPreference) { rememberPreference.value = this.checked; }
  });

  $('save-preferences').addEventListener('click', async function () {
    if (preferencesSave || !status || !rememberPreference) { return; }
    var product = $('default-view').dataset.product;
    var draft = preferenceDrafts.get(product);
    var submission = Object.freeze({
      product: product,
      defaultView: draft.value,
      rememberRecentFiles: rememberPreference.value
    });
    preferencesSave = submission;
    renderPreviewOptions();
    try {
      var saved = await api('/api/preferences', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(submission)
      });
      // Acknowledge only the submitted values; newer edits remain in the draft.
      draft.saved = submission.defaultView;
      rememberPreference.saved = submission.rememberRecentFiles;
      draft.message = saved.message || 'Preferences saved.';
      draft.warning = saved.code === 'PREVIEW_REFRESH_FAILED';
      toast(draft.message, draft.warning);
    } catch (error) {
      draft.message = error.message;
      draft.warning = true;
      toast(error.message, true);
    } finally {
      preferencesSave = null;
      renderPreviewOptions();
    }
    await loadStatus();
  });

  $('copy-diagnostics').addEventListener('click', async function () {
    var text = status.diagnostics.map(function (item) {
      return item.name + ': ' + item.status + ' - ' + item.detail;
    }).join('\n');
    try {
      await navigator.clipboard.writeText(text);
      toast('Diagnostic report copied.');
    } catch (_) {
      toast('Clipboard access is unavailable.', true);
    }
  });

  $('clear-local-data').addEventListener('click', async function () {
    try {
      await api('/api/local-data/clear', { method: 'POST' });
      toast('Local App history, uploads, and logs cleared.');
      await loadStatus();
    } catch (error) {
      toast(error.message, true);
    }
  });

  $('quit-app').addEventListener('click', async function () {
    try {
      await api('/api/stop', { method: 'POST' });
      document.body.replaceChildren();
      var main = node('main');
      var stopped = node('section', 'empty-state');
      stopped.append(
        node('h1', '', 'Aspose File Workspace has stopped.'),
        node('p', '', 'You can close this tab and start the CLI again when needed.'));
      main.append(stopped);
      document.body.append(main);
    } catch (error) {
      toast(error.message, true);
    }
  });

  applyTheme(readTheme());
  renderRoute(false);
  loadStatus();
}());
