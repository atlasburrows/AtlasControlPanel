window.atlasCharts = {
    addAreaFill: function (containerId) {
        var container = document.getElementById(containerId);
        if (!container) return;

        var svg = container.querySelector('svg');
        if (!svg) return;

        // Remove any existing area fill
        svg.querySelectorAll('.area-chart-fill, defs.area-defs').forEach(function (el) { el.remove(); });

        // Find ALL paths and pick the one that looks like a data line
        // (has multiple points, has a stroke, isn't a fill-only shape)
        var paths = svg.querySelectorAll('path');
        var linePath = null;
        var maxPoints = 0;
        paths.forEach(function (p) {
            var d = p.getAttribute('d') || '';
            var pointCount = (d.match(/[LlMm]/g) || []).length;
            if (pointCount > maxPoints) {
                maxPoints = pointCount;
                linePath = p;
            }
        });
        if (!linePath || maxPoints < 2) return;

        var pathData = linePath.getAttribute('d');

        // Extract all coordinate pairs from the path
        var coords = [];
        var re = /([-\d.]+)[,\s]+([-\d.]+)/g;
        var m;
        while ((m = re.exec(pathData)) !== null) {
            coords.push({ x: parseFloat(m[1]), y: parseFloat(m[2]) });
        }
        if (coords.length < 2) return;

        var firstX = coords[0].x;
        var lastX = coords[coords.length - 1].x;

        // Find the bottom of the plot area:
        // Look for horizontal lines (grid lines) — the one with the highest Y is the baseline
        var bottomY = 0;
        var horizLines = svg.querySelectorAll('line');
        horizLines.forEach(function (line) {
            var y1 = parseFloat(line.getAttribute('y1') || 0);
            var y2 = parseFloat(line.getAttribute('y2') || 0);
            if (Math.abs(y1 - y2) < 0.5 && y1 > bottomY) {
                bottomY = y1;
            }
        });

        // If no grid lines found, use the SVG height minus some padding
        if (bottomY === 0) {
            // Try text elements — x-axis labels have the highest Y
            var texts = svg.querySelectorAll('text');
            var maxTextY = 0;
            texts.forEach(function (t) {
                var ty = parseFloat(t.getAttribute('y') || 0);
                if (ty > maxTextY) maxTextY = ty;
            });
            if (maxTextY > 0) {
                bottomY = maxTextY - 15; // Above the labels
            } else {
                var svgHeight = svg.viewBox && svg.viewBox.baseVal ? svg.viewBox.baseVal.height : svg.clientHeight;
                bottomY = (svgHeight || 500) * 0.82;
            }
        }

        // Ensure bottomY is below all data points
        coords.forEach(function (c) {
            if (c.y > bottomY) bottomY = c.y;
        });

        // Build area path: original line path + close along bottom
        var areaPath = pathData + ' L ' + lastX + ',' + bottomY + ' L ' + firstX + ',' + bottomY + ' Z';

        // Create gradient
        var ns = 'http://www.w3.org/2000/svg';
        var defs = document.createElementNS(ns, 'defs');
        defs.setAttribute('class', 'area-defs');

        var grad = document.createElementNS(ns, 'linearGradient');
        grad.setAttribute('id', 'areaGradient-' + containerId);
        grad.setAttribute('x1', '0'); grad.setAttribute('y1', '0');
        grad.setAttribute('x2', '0'); grad.setAttribute('y2', '1');

        // Use stop-color + stop-opacity (more compatible than rgba in SVG)
        var stops = [
            {offset: '0%', color: '#3b82f6', opacity: '0.8'},
            {offset: '25%', color: '#3b82f6', opacity: '0.45'},
            {offset: '50%', color: '#3b82f6', opacity: '0.2'},
            {offset: '75%', color: '#3b82f6', opacity: '0.08'},
            {offset: '100%', color: '#3b82f6', opacity: '0.01'}
        ];
        stops.forEach(function (s) {
            var stop = document.createElementNS(ns, 'stop');
            stop.setAttribute('offset', s.offset);
            stop.setAttribute('stop-color', s.color);
            stop.setAttribute('stop-opacity', s.opacity);
            grad.appendChild(stop);
        });

        defs.appendChild(grad);
        svg.insertBefore(defs, svg.firstChild);

        // Create fill path
        var fillPath = document.createElementNS(ns, 'path');
        fillPath.setAttribute('d', areaPath);
        fillPath.setAttribute('class', 'area-chart-fill');
        fillPath.setAttribute('fill', 'url(#areaGradient-' + containerId + ')');
        fillPath.setAttribute('stroke', 'none');

        // Insert right before the line's parent so it's behind the line
        var parent = linePath.parentNode;
        parent.insertBefore(fillPath, linePath);

        // Crop SVG viewBox to tightly wrap the chart content
        var allTexts = svg.querySelectorAll('text');
        var minTextY = 9999, maxTextY = 0;
        allTexts.forEach(function (t) {
            var ty = parseFloat(t.getAttribute('y') || 0);
            if (ty > 0 && ty < minTextY) minTextY = ty;
            if (ty > maxTextY) maxTextY = ty;
        });
        var cropTop = Math.max(0, minTextY - 14);
        var cropBottom = maxTextY > 0 ? maxTextY + 14 : bottomY + 30;
        var vb = svg.getAttribute('viewBox');
        if (vb) {
            var parts = vb.split(/\s+/);
            var vbW = parseFloat(parts[2]);
            svg.setAttribute('viewBox', '0 ' + cropTop + ' ' + vbW + ' ' + (cropBottom - cropTop));
            // Stretch SVG to fill container — don't preserve aspect ratio
            svg.setAttribute('preserveAspectRatio', 'none');
            svg.style.height = '100%';
        }
    }
};
