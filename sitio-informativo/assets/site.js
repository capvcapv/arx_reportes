const menuButton = document.querySelector('.menu-button');
const siteMenu = document.querySelector('.site-menu');

if (menuButton && siteMenu) {
    menuButton.addEventListener('click', () => {
        const open = menuButton.getAttribute('aria-expanded') === 'true';
        menuButton.setAttribute('aria-expanded', String(!open));
        siteMenu.classList.toggle('is-open', !open);
    });

    siteMenu.addEventListener('click', (event) => {
        if (event.target.closest('a')) {
            menuButton.setAttribute('aria-expanded', 'false');
            siteMenu.classList.remove('is-open');
        }
    });
}

document.querySelectorAll('[data-year]').forEach((element) => {
    element.textContent = new Date().getFullYear();
});

document.querySelectorAll('.copy-code').forEach((button) => {
    button.addEventListener('click', async () => {
        const code = button.parentElement?.querySelector('code')?.textContent ?? '';
        try {
            await navigator.clipboard.writeText(code);
            const original = button.textContent;
            button.textContent = 'Copiado';
            setTimeout(() => { button.textContent = original; }, 1600);
        } catch {
            button.textContent = 'Selecciona el texto';
        }
    });
});

const search = document.querySelector('#docs-search');
if (search) {
    const sections = [...document.querySelectorAll('[data-doc-section]')];
    const emptyMessage = document.querySelector('.no-results');
    search.addEventListener('input', () => {
        const term = search.value.trim().toLocaleLowerCase('es-MX');
        let visible = 0;
        sections.forEach((section) => {
            const matches = !term || section.textContent.toLocaleLowerCase('es-MX').includes(term);
            section.hidden = !matches;
            if (matches) visible += 1;
        });
        if (emptyMessage) emptyMessage.hidden = visible > 0;
    });
}
