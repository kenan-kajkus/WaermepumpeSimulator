window.PlotlyInterop = {
    newPlot: function (elementId, traces, layout, config) {
        var el = document.getElementById(elementId);
        if (!el) return;
        Plotly.newPlot(el, JSON.parse(traces), JSON.parse(layout), config ? JSON.parse(config) : { responsive: true });
    },
    purge: function (elementId) {
        var el = document.getElementById(elementId);
        if (el) Plotly.purge(el);
    }
};
