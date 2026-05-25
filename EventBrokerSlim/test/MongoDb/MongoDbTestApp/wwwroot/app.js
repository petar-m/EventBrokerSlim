(function () {
    const publishBtn = document.getElementById("publishBtn");
    const publishStatus = document.getElementById("publishStatus");
    const connectionIndicator = document.getElementById("connectionIndicator");
    const connectionText = document.getElementById("connectionText");
    const eventList = document.getElementById("eventList");

    let eventSource = null;

    function connectSSE() {
        if (eventSource) {
            eventSource.close();
        }

        eventSource = new EventSource("/api/events/stream");

        eventSource.onopen = function () {
            connectionIndicator.className = "indicator connected";
            connectionText.textContent = "Connected (SSE)";
        };

        eventSource.onmessage = function (e) {
            const event = JSON.parse(e.data);
            addEventCard(event);
        };

        eventSource.onerror = function () {
            connectionIndicator.className = "indicator disconnected";
            connectionText.textContent = "Disconnected — reconnecting...";
        };
    }

    function addEventCard(event) {
        const placeholder = eventList.querySelector(".placeholder");
        if (placeholder) {
            placeholder.remove();
        }

        const publishedAt = new Date(event.publishedAt);
        const handledAt = new Date(event.handledAt);
        const latencyMs = handledAt - publishedAt;

        const card = document.createElement("div");
        card.className = "event-card";
        card.innerHTML =
            '<div class="message">' + escapeHtml(event.message) + '</div>' +
            '<div class="timestamps">' +
                '<span>Published: ' + formatTime(publishedAt) + '</span>' +
                '<span>Handled: ' + formatTime(handledAt) + '</span>' +
                '<span class="latency">Latency: ' + latencyMs + ' ms</span>' +
            '</div>';

        eventList.prepend(card);
    }

    function formatTime(date) {
        return date.toLocaleTimeString(undefined, {
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
            fractionalSecondDigits: 3,
        });
    }

    function escapeHtml(text) {
        var div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }

    publishBtn.addEventListener("click", async function () {
        publishBtn.disabled = true;
        publishStatus.textContent = "Publishing...";

        try {
            const response = await fetch("/api/publish", { method: "POST" });
            if (response.ok) {
                const result = await response.json();
                publishStatus.textContent = "Published: " + result.message;
            } else {
                publishStatus.textContent = "Error: " + response.statusText;
            }
        } catch (err) {
            publishStatus.textContent = "Error: " + err.message;
        } finally {
            publishBtn.disabled = false;
        }
    });

    connectSSE();
})();
