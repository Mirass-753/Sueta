mergeInto(LibraryManager.library, {
    webSocketSafeCall: function (cb, res) {
        if (!cb) return;
        if (typeof Module !== 'undefined' && Module['dynCall_vi']) {
            Module['dynCall_vi'](cb, res);
        } else if (typeof wasmTable !== 'undefined') {
            try {
                var func = wasmTable.get(cb);
                if (typeof func === 'function') func(res);
            } catch (e) {}
        }
    }
});