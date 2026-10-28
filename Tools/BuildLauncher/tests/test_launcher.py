import http.client,importlib.util,json,ssl,sys,tempfile,threading,unittest,urllib.error,urllib.request,zipfile
from functools import partial
from pathlib import Path
from unittest.mock import patch,MagicMock

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import artifacts,network,playsolana,serve

class Tests(unittest.TestCase):
    def test_https_uses_launcher_identifier(self):
        opener=MagicMock()
        with patch.object(network.urllib.request,'build_opener',return_value=opener):
            network.urlopen('https://assets.battlecities.com/file')
        request=opener.open.call_args.args[0]
        self.assertEqual(request.get_header('User-agent'),'BattleCities-BuildLauncher/1.0')
    def test_portable_web_archive(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d)/'site';(root/'Build').mkdir(parents=True)
            (root/'index.html').write_text('<html>fixture</html>');(root/'Build/game.loader.js').write_text('// fixture')
            out=Path(d)/'site.zip';artifacts.web_zip(root,out)
            with zipfile.ZipFile(out) as z: self.assertEqual(set(z.namelist()),{'index.html','Build/game.loader.js'})
            with self.assertRaises(ValueError): artifacts.web_zip(root,out)
            with self.assertRaises(ValueError): artifacts.web_zip(root,root/'nested.zip')
    def test_local_server_identity_and_no_listing(self):
        with tempfile.TemporaryDirectory() as d:
            root=Path(d);(root/'index.html').write_text('actual fixture build');(root/'empty').mkdir()
            server=serve.ThreadingHTTPServer(('127.0.0.1',0),partial(serve.Handler,directory=d))
            server.build_identity='new-build';server.build_root=root.resolve()
            thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
            url=f'http://127.0.0.1:{server.server_port}'
            try:
                with urllib.request.urlopen(url+'/__battlecities_build__') as r: self.assertEqual(json.load(r)['identity'],'new-build')
                with urllib.request.urlopen(url+'/') as r:
                    self.assertEqual(r.read(),b'actual fixture build');self.assertEqual(r.headers['Cache-Control'],'no-store')
                with self.assertRaises(urllib.error.HTTPError) as error: urllib.request.urlopen(url+'/empty/')
                self.assertEqual(error.exception.code,403)
            finally: server.shutdown();server.server_close();thread.join()
    def test_mcp_json_and_sse(self):
        value={'jsonrpc':'2.0','id':1,'result':{'ok':True}}
        self.assertEqual(playsolana.decode_rpc(json.dumps(value).encode()),value)
        self.assertEqual(playsolana.decode_rpc(('event: message\r\ndata: '+json.dumps(value)+'\r\n\r\n').encode()),value)
    def test_upload_form_is_parsed_without_execution(self):
        cmd="curl -X POST 'https://bucket.sfo3.digitaloceanspaces.com/' -F 'key=test.apk' -F 'Policy=abc' -F 'X-Amz-Signature=secret' -F 'file=@/do/not/read.apk'"
        self.assertEqual(playsolana.parse_upload_form(cmd),{'key':'test.apk','Policy':'abc','X-Amz-Signature':'secret'})
        with self.assertRaises(ValueError): playsolana.parse_upload_form("curl -F key=x -F Policy=y -F 'other=@/private/file'")
        with self.assertRaises(ValueError): playsolana.parse_upload_form('curl -F key=x')
    def test_storage_destination_checks(self):
        playsolana.storage_url('https://ps-games.sfo3.digitaloceanspaces.com/object')
        for url in ['http://ps-games.sfo3.digitaloceanspaces.com/x','https://attacker.test/x','https://user:password@ps-games.sfo3.digitaloceanspaces.com/x']:
            with self.assertRaises(ValueError): playsolana.storage_url(url)
    def test_only_draft_with_verified_wallet(self):
        client=MagicMock();client.call.return_value={'submission':{'id':'draft','status':'DRAFT','authorityWalletVerified':True}}
        self.assertEqual(playsolana.current_draft(client,'draft')['id'],'draft')
        for status,verified in [('APPROVED',True),('DRAFT',False)]:
            client.call.return_value={'submission':{'status':status,'authorityWalletVerified':verified}}
            with self.assertRaises(RuntimeError): playsolana.current_draft(client,'draft')
    def test_doh_fallback_retains_original_tls_hostname(self):
        context=MagicMock();raw=MagicMock();tls=MagicMock();context.wrap_socket.return_value=tls
        conn=network.HTTPSConnection('assets.battlecities.com',context=context)
        with patch.object(http.client.HTTPSConnection,'connect',side_effect=network.socket.gaierror('lookup failed')),patch.object(network,'fallback_addresses',return_value=['1.1.1.1']),patch.object(network.socket,'create_connection',return_value=raw):
            conn.connect()
        context.wrap_socket.assert_called_once_with(raw,server_hostname='assets.battlecities.com')
        self.assertIs(conn.sock,tls)
    def test_normal_resolution_does_not_contact_doh(self):
        conn=network.HTTPSConnection('assets.battlecities.com')
        with patch.object(http.client.HTTPSConnection,'connect'),patch.object(network,'fallback_addresses') as fallback: conn.connect()
        fallback.assert_not_called()
    def test_ota_failure_does_not_write_a_success_receipt(self):
        with tempfile.TemporaryDirectory() as d:
            manifest=Path(d)/'content-manifest.json';manifest.write_text(json.dumps({'url':'https://assets.battlecities.com/android/art/'+'a'*64+'.bundle','sha256':'a'*64,'bytes':3}))
            response=MagicMock();response.__enter__.return_value=response;response.status=200;response.url=json.loads(manifest.read_text())['url'];response.read.side_effect=[b'bad',b'']
            with patch.object(artifacts.network,'urlopen',return_value=response),self.assertRaises(ValueError): artifacts.verify_ota(manifest)
            self.assertFalse(manifest.with_name('hosting-verification.json').exists())

if __name__=='__main__': unittest.main()
