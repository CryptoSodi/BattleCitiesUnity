"""HTTPS with a process-local DoH fallback; never changes device DNS settings.

Only used after a normal hostname lookup fails. TLS still authenticates the
original hostname; no hardcoded game/CDN server IPs or disabled certificates.
"""
import http.client,ipaddress,json,socket,ssl,urllib.parse,urllib.request

def fallback_addresses(host):
    query=urllib.parse.urlencode({'name':host,'type':'A'})
    request=urllib.request.Request('https://1.1.1.1/dns-query?'+query,headers={'Accept':'application/dns-json'})
    with urllib.request.urlopen(request,timeout=15) as response: data=json.load(response)
    if data.get('Status')!=0: raise OSError('HTTPS DNS lookup failed')
    addresses=[row['data'] for row in data.get('Answer',[]) if row.get('type')==1]
    if not addresses or any(not ipaddress.ip_address(ip).is_global for ip in addresses):
        raise OSError('No public IPv4 addresses returned by HTTPS DNS')
    return addresses

class HTTPSConnection(http.client.HTTPSConnection):
    def connect(self):
        try: return super().connect()
        except socket.gaierror:
            if self._tunnel_host: raise
        last=None
        for address in fallback_addresses(self.host):
            raw=None
            try:
                raw=socket.create_connection((address,self.port),self.timeout,self.source_address)
                self.sock=self._context.wrap_socket(raw,server_hostname=self.host)
                return
            except OSError as error:
                if raw: raw.close()
                last=error
        raise OSError('Could not connect after HTTPS DNS fallback') from last

class Handler(urllib.request.HTTPSHandler):
    def https_open(self,request):
        return self.do_open(HTTPSConnection,request,context=ssl.create_default_context())

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs): return None

def urlopen(request,timeout=180):
    return urllib.request.build_opener(Handler(),NoRedirect()).open(request,timeout=timeout)
