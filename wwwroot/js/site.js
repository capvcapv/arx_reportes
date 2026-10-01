(() => {
    const palettes = {
        verde: ['#2f8f5b', '#7bc043', '#176b57', '#a5cf63', '#49a98a', '#d2a83e'],
        azul: ['#34729c', '#58a6c8', '#31588d', '#76c5d5', '#6b73b7', '#9bc6e0'],
        mixta: ['#2f8f5b', '#34729c', '#d2a83e', '#a94c4c', '#765aa6', '#49a98a'],
        calida: ['#c8812f', '#d3aa3c', '#b85646', '#df7757', '#a65f35', '#e1bb75'],
        grafito: ['#46565e', '#76858b', '#283940', '#9ca8ac', '#60747a', '#bcc4c6']
    };

    const numberFormat = (value, format, decimals, compact = false) => {
        if (value === null || value === undefined || Number.isNaN(Number(value))) return '—';
        const options = compact
            ? { notation: 'compact', maximumFractionDigits: 1 }
            : { minimumFractionDigits: decimals, maximumFractionDigits: decimals };
        if (format === 'moneda') Object.assign(options, { style: 'currency', currency: 'MXN' });
        const formatted = new Intl.NumberFormat('es-MX', options).format(Number(value));
        return format === 'porcentaje' ? `${formatted}%` : formatted;
    };

    class ArxCanvasChart {
        constructor(host) {
            this.host = host;
            this.canvas = host.querySelector('canvas');
            this.context = this.canvas?.getContext('2d');
            this.tooltip = host.querySelector('.chart-tooltip');
            this.legend = host.querySelector('.chart-legend');
            this.hitRegions = [];
            try { this.data = JSON.parse(host.dataset.chart || '{}'); }
            catch { this.data = null; }
            if (!this.context || !this.data) return;

            this.colors = palettes[this.data.palette] || palettes.verde;
            this.canvas.addEventListener('pointermove', event => this.handlePointer(event));
            this.canvas.addEventListener('pointerleave', () => this.hideTooltip());
            this.resizeObserver = new ResizeObserver(() => this.render());
            this.resizeObserver.observe(this.canvas.parentElement);
            this.render();
        }

        render() {
            const bounds = this.canvas.getBoundingClientRect();
            if (bounds.width < 40 || bounds.height < 40) return;
            const ratio = Math.min(window.devicePixelRatio || 1, 2);
            this.canvas.width = Math.round(bounds.width * ratio);
            this.canvas.height = Math.round(bounds.height * ratio);
            this.context.setTransform(ratio, 0, 0, ratio, 0, 0);
            this.context.clearRect(0, 0, bounds.width, bounds.height);
            this.width = bounds.width;
            this.height = bounds.height;
            this.hitRegions = [];

            const hasValues = (this.data.dataSets || []).some(set => (set.values || []).some(value => value !== null && Number.isFinite(Number(value))));
            if (!hasValues) {
                this.drawEmpty();
                this.renderLegend([]);
                return;
            }

            if (this.data.type === 'dona') this.drawDoughnut();
            else this.drawCartesian();
        }

        drawEmpty() {
            const ctx = this.context;
            ctx.save();
            ctx.fillStyle = '#78877f';
            ctx.font = '600 13px -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText('No hay valores para mostrar', this.width / 2, this.height / 2);
            ctx.restore();
        }

        drawCartesian() {
            const ctx = this.context;
            const labels = this.data.labels || [];
            const sets = this.data.dataSets || [];
            const values = sets.flatMap(set => (set.values || []).filter(value => value !== null).map(Number));
            const plot = { x: 65, y: 18, width: Math.max(20, this.width - 82), height: Math.max(20, this.height - 68) };
            let minimum = Math.min(...values);
            let maximum = Math.max(...values);
            const includeZero = this.data.type === 'barras' || this.data.type === 'area';
            if (includeZero) {
                minimum = Math.min(0, minimum);
                maximum = Math.max(0, maximum);
            }
            if (minimum === maximum) {
                const padding = Math.abs(maximum || 1) * .2;
                minimum -= padding;
                maximum += padding;
            } else if (!includeZero) {
                const padding = (maximum - minimum) * .08;
                minimum -= padding;
                maximum += padding;
            }

            const toY = value => plot.y + plot.height - ((value - minimum) / (maximum - minimum)) * plot.height;
            const zeroY = toY(Math.min(maximum, Math.max(minimum, 0)));
            this.drawAxes(plot, labels, minimum, maximum, toY);

            if (this.data.type === 'barras') {
                const groupWidth = plot.width / Math.max(labels.length, 1);
                const availableWidth = Math.min(groupWidth * .72, 78);
                const barWidth = Math.max(2, availableWidth / Math.max(sets.length, 1));
                sets.forEach((set, setIndex) => {
                    (set.values || []).forEach((rawValue, index) => {
                        if (rawValue === null || !Number.isFinite(Number(rawValue))) return;
                        const value = Number(rawValue);
                        const x = plot.x + index * groupWidth + (groupWidth - availableWidth) / 2 + setIndex * barWidth;
                        const valueY = toY(value);
                        const y = Math.min(valueY, zeroY);
                        const height = Math.max(1, Math.abs(zeroY - valueY));
                        ctx.fillStyle = this.colors[setIndex % this.colors.length];
                        ctx.fillRect(x + 1, y, Math.max(1, barWidth - 2), height);
                        this.hitRegions.push({ kind: 'rect', x, y, width: barWidth, height, label: labels[index], series: set.name, value, color: ctx.fillStyle });
                    });
                });
            } else {
                sets.forEach((set, setIndex) => {
                    const color = this.colors[setIndex % this.colors.length];
                    const points = (set.values || []).map((rawValue, index) => {
                        if (rawValue === null || !Number.isFinite(Number(rawValue))) return null;
                        const x = labels.length === 1 ? plot.x + plot.width / 2 : plot.x + index * plot.width / Math.max(labels.length - 1, 1);
                        return { x, y: toY(Number(rawValue)), value: Number(rawValue), label: labels[index] };
                    });
                    const validPoints = points.filter(Boolean);
                    if (validPoints.length === 0) return;

                    ctx.beginPath();
                    validPoints.forEach((point, index) => index === 0 ? ctx.moveTo(point.x, point.y) : ctx.lineTo(point.x, point.y));
                    if (this.data.type === 'area') {
                        ctx.lineTo(validPoints[validPoints.length - 1].x, zeroY);
                        ctx.lineTo(validPoints[0].x, zeroY);
                        ctx.closePath();
                        ctx.fillStyle = this.withAlpha(color, .13);
                        ctx.fill();
                        ctx.beginPath();
                        validPoints.forEach((point, index) => index === 0 ? ctx.moveTo(point.x, point.y) : ctx.lineTo(point.x, point.y));
                    }
                    ctx.strokeStyle = color;
                    ctx.lineWidth = 2.5;
                    ctx.lineJoin = 'round';
                    ctx.lineCap = 'round';
                    ctx.stroke();

                    validPoints.forEach(point => {
                        ctx.beginPath();
                        ctx.arc(point.x, point.y, 3.8, 0, Math.PI * 2);
                        ctx.fillStyle = '#f7fbf8';
                        ctx.fill();
                        ctx.lineWidth = 2;
                        ctx.strokeStyle = color;
                        ctx.stroke();
                        this.hitRegions.push({ kind: 'point', x: point.x, y: point.y, radius: 12, label: point.label, series: set.name, value: point.value, color });
                    });
                });
            }

            this.renderLegend(sets.map((set, index) => ({ label: set.name, color: this.colors[index % this.colors.length] })));
        }

        drawAxes(plot, labels, minimum, maximum, toY) {
            const ctx = this.context;
            ctx.save();
            ctx.font = '11px -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
            ctx.textBaseline = 'middle';
            const ticks = 5;
            for (let index = 0; index <= ticks; index++) {
                const value = minimum + (maximum - minimum) * index / ticks;
                const y = toY(value);
                ctx.beginPath();
                ctx.moveTo(plot.x, y);
                ctx.lineTo(plot.x + plot.width, y);
                ctx.strokeStyle = 'rgba(45,82,69,.11)';
                ctx.lineWidth = 1;
                ctx.stroke();
                ctx.fillStyle = '#829087';
                ctx.textAlign = 'right';
                ctx.fillText(numberFormat(value, this.data.format, this.data.decimals, true), plot.x - 9, y);
            }

            const maximumLabels = Math.max(2, Math.floor(plot.width / 76));
            const step = Math.max(1, Math.ceil(labels.length / maximumLabels));
            ctx.textAlign = 'center';
            ctx.textBaseline = 'top';
            ctx.fillStyle = '#74847b';
            labels.forEach((label, index) => {
                if (index % step !== 0 && index !== labels.length - 1) return;
                const x = this.data.type === 'barras'
                    ? plot.x + (index + .5) * plot.width / Math.max(labels.length, 1)
                    : labels.length === 1 ? plot.x + plot.width / 2 : plot.x + index * plot.width / Math.max(labels.length - 1, 1);
                const text = String(label).length > 13 ? `${String(label).slice(0, 12)}…` : String(label);
                ctx.fillText(text, x, plot.y + plot.height + 12);
            });
            ctx.restore();
        }

        drawDoughnut() {
            const ctx = this.context;
            const labels = this.data.labels || [];
            const slices = [];
            (this.data.dataSets || []).forEach((set, setIndex) => {
                (set.values || []).forEach((rawValue, index) => {
                    if (rawValue === null || !Number.isFinite(Number(rawValue)) || Number(rawValue) === 0) return;
                    const baseLabel = labels[index] ?? `Dato ${index + 1}`;
                    const label = this.data.dataSets.length > 1 ? `${baseLabel} · ${set.name}` : baseLabel;
                    slices.push({ label, series: set.name, value: Number(rawValue), magnitude: Math.abs(Number(rawValue)), color: this.colors[(index + setIndex) % this.colors.length] });
                });
            });
            const total = slices.reduce((sum, slice) => sum + slice.magnitude, 0);
            if (!total) return this.drawEmpty();

            const centerX = this.width / 2;
            const centerY = this.height / 2;
            const outerRadius = Math.max(45, Math.min(this.width, this.height) * .34);
            const innerRadius = outerRadius * .61;
            let angle = -Math.PI / 2;
            slices.forEach(slice => {
                const nextAngle = angle + Math.PI * 2 * slice.magnitude / total;
                ctx.beginPath();
                ctx.arc(centerX, centerY, outerRadius, angle, nextAngle);
                ctx.arc(centerX, centerY, innerRadius, nextAngle, angle, true);
                ctx.closePath();
                ctx.fillStyle = slice.color;
                ctx.fill();
                ctx.strokeStyle = 'rgba(255,255,255,.7)';
                ctx.lineWidth = 2;
                ctx.stroke();
                this.hitRegions.push({ kind: 'arc', centerX, centerY, innerRadius, outerRadius, start: angle, end: nextAngle, ...slice });
                angle = nextAngle;
            });

            ctx.fillStyle = '#29463d';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.font = '700 18px -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
            ctx.fillText(numberFormat(total, this.data.format, this.data.decimals, true), centerX, centerY - 5);
            ctx.fillStyle = '#839087';
            ctx.font = '650 10px -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
            ctx.fillText('TOTAL', centerX, centerY + 15);
            this.renderLegend(slices.map(slice => ({ label: slice.label, color: slice.color })));
        }

        renderLegend(items) {
            this.legend.replaceChildren();
            if (!this.data.showLegend || items.length === 0) return;
            items.slice(0, 18).forEach(item => {
                const container = document.createElement('span');
                container.className = 'chart-legend-item';
                const swatch = document.createElement('i');
                swatch.className = 'chart-legend-swatch';
                swatch.style.backgroundColor = item.color;
                const label = document.createElement('span');
                label.textContent = item.label;
                container.append(swatch, label);
                this.legend.append(container);
            });
        }

        handlePointer(event) {
            const bounds = this.canvas.getBoundingClientRect();
            const x = event.clientX - bounds.left;
            const y = event.clientY - bounds.top;
            const match = this.hitRegions.find(region => {
                if (region.kind === 'rect') return x >= region.x && x <= region.x + region.width && y >= region.y && y <= region.y + region.height;
                if (region.kind === 'point') return Math.hypot(x - region.x, y - region.y) <= region.radius;
                if (region.kind === 'arc') {
                    const distance = Math.hypot(x - region.centerX, y - region.centerY);
                    let angle = Math.atan2(y - region.centerY, x - region.centerX);
                    if (angle < -Math.PI / 2) angle += Math.PI * 2;
                    return distance >= region.innerRadius && distance <= region.outerRadius && angle >= region.start && angle <= region.end;
                }
                return false;
            });
            if (!match) return this.hideTooltip();
            this.showTooltip(match, x, y);
        }

        showTooltip(region, x, y) {
            const title = document.createElement('strong');
            title.textContent = region.label;
            const detail = document.createElement('span');
            const series = region.series && region.series !== 'Valor' ? `${region.series}: ` : '';
            detail.textContent = `${series}${numberFormat(region.value, this.data.format, this.data.decimals)}`;
            this.tooltip.replaceChildren(title, detail);
            this.tooltip.style.left = `${Math.min(Math.max(x, 70), this.width - 70)}px`;
            this.tooltip.style.top = `${Math.max(y, 65)}px`;
            this.tooltip.classList.add('is-visible');
        }

        hideTooltip() {
            this.tooltip?.classList.remove('is-visible');
        }

        withAlpha(hex, alpha) {
            const value = hex.replace('#', '');
            const red = parseInt(value.slice(0, 2), 16);
            const green = parseInt(value.slice(2, 4), 16);
            const blue = parseInt(value.slice(4, 6), 16);
            return `rgba(${red},${green},${blue},${alpha})`;
        }
    }

    document.querySelectorAll('.arx-chart').forEach(host => new ArxCanvasChart(host));
})();
