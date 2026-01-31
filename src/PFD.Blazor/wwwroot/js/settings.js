// Scroll sync for time roller
window.syncScroll = function (source, target) {
    if (source && target) {
        target.scrollTop = source.scrollTop;
    }
};

window.scrollToHour = function (timeRoller, tasksPanel, hour) {
    const slotHeight = 50; // Height of each time slot
    const scrollPosition = hour * slotHeight;

    if (timeRoller) {
        timeRoller.scrollTop = scrollPosition;
    }
    if (tasksPanel) {
        tasksPanel.scrollTop = scrollPosition;
    }
};

window.settingsInterop = {
    getDeviceId: function () {
        let deviceId = localStorage.getItem('pfd_device_id');
        if (!deviceId) {
            deviceId = 'device_' + Date.now() + '_' + Math.random().toString(36).substring(2, 11);
            localStorage.setItem('pfd_device_id', deviceId);
        }
        return deviceId;
    },

    getTheme: function () {
        return localStorage.getItem('pfd_theme') || 'teal';
    },

    setTheme: function (theme) {
        localStorage.setItem('pfd_theme', theme);
    },

    getIsDailyView: function () {
        const value = localStorage.getItem('pfd_is_daily_view');
        return value === null ? true : value === 'true';
    },

    setIsDailyView: function (isDailyView) {
        localStorage.setItem('pfd_is_daily_view', isDailyView.toString());
    },

    getUseLargeText: function () {
        const value = localStorage.getItem('pfd_use_large_text');
        return value === 'true';
    },

    setUseLargeText: function (useLargeText) {
        localStorage.setItem('pfd_use_large_text', useLargeText.toString());
    },

    getHighContrast: function () {
        const value = localStorage.getItem('pfd_high_contrast');
        return value === 'true';
    },

    setHighContrast: function (highContrast) {
        localStorage.setItem('pfd_high_contrast', highContrast.toString());
    }
};
