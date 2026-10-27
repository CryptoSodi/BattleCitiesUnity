"""Loopback-only Unity WebGL server. No directory listings or remote binding."""
import argparse,json,mimetypes
from functools import partial
from http.server import SimpleHTTPRequestHandler,ThreadingHTTPServer
from pathlib import Path

class Handler(SimpleHTTPRequestHandler):
    def list_directory(self,path):
        self.send_error(403,'Directory listings disabled')
    def end_headers(self):
        self.send_header('Cache-Control','no-store')
        super().end_headers()
    def do_GET(self):
        if self.path.split('?',1)[0]=='/__battlecities_build__':
            body=json.dumps({'identity':self.server.build_identity}).encode()
            self.send_response(200);self.send_header('Content-Type','application/json')
            self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
            return
        target=Path(self.translate_path(self.path)).resolve()
        if not target.is_relative_to(self.server.build_root):
            self.send_error(403);return
        super().do_GET()

def main():
    p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--port',type=int,default=8080);p.add_argument('--identity',required=True)
    args=p.parse_args();root=Path(args.root).resolve()
    if not (root/'index.html').is_file(): p.error('Missing built index.html')
    mimetypes.add_type('application/wasm','.wasm');mimetypes.add_type('application/octet-stream','.unityweb')
    server=ThreadingHTTPServer(('127.0.0.1',args.port),partial(Handler,directory=str(root)))
    server.build_identity=args.identity;server.build_root=root
    print(f'Serving {root} at http://localhost:{args.port}/',flush=True)
    try: server.serve_forever()
    finally: server.server_close()

if __name__=='__main__': main()
