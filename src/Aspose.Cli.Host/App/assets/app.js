(function () {
  'use strict';

  var csrfMeta = document.querySelector('meta[name="aspose-csrf"]');
  var csrf = csrfMeta ? csrfMeta.content : '';
  var status = null;
  var polling = null;
  var licenseTarget = null;
  var $ = function (id) { return document.getElementById(id); };

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
        var heading = $(visible + '-view').querySelector('h1');
        if (heading) {
          heading.focus({ preventScroll: true });
        }
      });
    }
  }

  async function loadStatus() {
    try {
      status = await api('/api/status', { method: 'GET' });
      renderStatus();
    } catch (error) {
      setActivity('Disconnected');
      toast(error.message, true);
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

    document.body.dataset.edition = status.edition;
    document.body.dataset.experience = status.experience;
    $('edition-chip').textContent = status.editionName;
    setActivity(status.file ? 'Preview live' : 'Ready');
    $('file-input').accept = (status.supportedExtensions || []).join(',');
    $('remember-recents').checked = status.rememberRecentFiles;
    renderEditionComposition();
    renderProducts();
    renderRecents();
    renderPreviewOptions();
    renderAgentCommands();
    renderDiagnostics();
    renderPreview();
  }

  function renderEditionComposition() {
    var bannerMount = $('edition-banner-mount');
    var settingsMount = $('edition-settings-mount');
    var previewMount = $('preview-edition-mount');
    bannerMount.replaceChildren();
    settingsMount.replaceChildren();
    previewMount.replaceChildren();
    if (status.experience !== 'licensed') {
      return;
    }

    var state = commercialState();
    var banner = node('aside', 'edition-banner');
    banner.dataset.ui = 'license-banner';
    if (state.broken) {
      banner.classList.add('error');
    } else if (!state.complete) {
      banner.classList.add('warn');
    }
    var copy = node('div');
    copy.append(
      node('span', 'eyebrow', 'COMMERCIAL EDITION'),
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
      if (product.skill) {
        card.append(node(
          'div',
          'skill-line',
          'aspose-cli skill install ' + product.skill.name + ' --host codex --scope project'));
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

  function renderPreviewOptions() {
    var select = $('default-view');
    select.replaceChildren();
    var views = status.previewViews || [];
    views.forEach(function (view) {
      var option = node('option', '', view.displayName);
      option.value = view.id;
      select.append(option);
    });
    select.value = status.defaultView;
  }

  function renderAgentCommands() {
    var root = $('agent-commands');
    root.replaceChildren();
    var products = (status.products || []).filter(function (product) {
      return Boolean(product.skill);
    });
    if (!products.length) {
      root.append(node('span', '', 'This build has no bundled Agent Skills.'));
      return;
    }
    products.forEach(function (product) {
      var command = node('div', 'agent-command');
      command.append(
        node('strong', '', product.name),
        node('span', '', 'aspose-cli skill install ' + product.skill.name + ' --host codex --scope project'));
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

  function renderPreview() {
    var product = currentProduct();
    $('preview-file').textContent = status.file || 'No file open';
    $('preview-kind').textContent = status.uploadedCopy
      ? 'Temporary local copy'
      : product
        ? product.name + ' / ' + product.preview.fidelity + ' preview'
        : 'Local preview';
    var frame = $('preview-frame');
    var empty = $('preview-empty');
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
      await api('/api/license', {
        method: 'POST',
        headers: headers,
        body: file
      });
      toast('License installed and the local preview refreshed.');
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
      toast('Saved license removed. Restarting the local App.');
      if (result.restartUrl) {
        location.replace(result.restartUrl);
      } else {
        await loadStatus();
      }
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

  $('save-preferences').addEventListener('click', async function () {
    try {
      var saved = await api('/api/preferences', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          product: status.product,
          defaultView: $('default-view').value,
          rememberRecentFiles: $('remember-recents').checked
        })
      });
      toast(saved.message || 'Preferences saved.', saved.code === 'PREVIEW_REFRESH_FAILED');
      await loadStatus();
    } catch (error) {
      toast(error.message, true);
    }
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

  renderRoute(false);
  loadStatus();
}());
