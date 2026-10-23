mergeInto(LibraryManager.library, {
  BattleCitiesSyncScreenCache: function() {
    if (typeof FS !== 'undefined' && FS.syncfs) FS.syncfs(false, function(error) {
      if (error) console.warn('Battle Cities screen cache could not be persisted. Bundled content remains available.');
    });
  },
  BattleCitiesVerifyScreenSignature: function(id, modulusPtr, exponentPtr, payloadPtr, signaturePtr, callback) {
    const modulus = UTF8ToString(modulusPtr), exponent = UTF8ToString(exponentPtr);
    const payload = UTF8ToString(payloadPtr), signature = UTF8ToString(signaturePtr);
    const bytes = value => Uint8Array.from(atob(value), c => c.charCodeAt(0));
    const url64 = value => value.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    (async () => {
      try {
        if (!globalThis.crypto || !crypto.subtle || payload.length > 44000 || signature.length > 2048) return false;
        if (bytes(modulus).length < 256) return false;
        const key = await crypto.subtle.importKey('jwk', {kty:'RSA', n:url64(modulus), e:url64(exponent), alg:'RS256', ext:true},
          {name:'RSASSA-PKCS1-v1_5', hash:'SHA-256'}, false, ['verify']);
        return await crypto.subtle.verify('RSASSA-PKCS1-v1_5', key, bytes(signature), bytes(payload));
      } catch (_) { return false; }
    })().then(valid => { {{{ makeDynCall('vii', 'callback') }}}(id, valid ? 1 : 0); });
  }
});
