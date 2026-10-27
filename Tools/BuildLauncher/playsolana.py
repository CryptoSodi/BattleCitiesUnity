"""PlayGate draft APK uploads through its authenticated MCP service.

No token is written to disk or sent to an upload/CDN host. No review submission.
"""
import argparse,hashlib,http.client,json,os,re,shlex,ssl,sys,urllib.parse,urllib.request,uuid
from pathlib import Path
import network

ENDPOINT='https://mcp.playsolana.com/mcp'

def token_from_environment():
    value=os.environ.get('PLAYSOLANA_MCP_TOKEN','').strip()
    if not value and os.name=='nt':
        import winreg
        try:
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER,'Environment') as k:
                value=winreg.QueryValueEx(k,'PLAYSOLANA_MCP_TOKEN')[0].strip()
        except FileNotFoundError: pass
    if not value: raise RuntimeError('Set PLAYSOLANA_MCP_TOKEN in your Windows user environment; do not put it in launcher settings.')
    return value

def decode_rpc(raw):
    text=raw.decode('utf-8-sig').strip()
    if not text: return None
    if text.startswith('{'): return json.loads(text)
    for event in text.replace('\r\n','\n').split('\n\n'):
        data='\n'.join(line[5:].lstrip() for line in event.splitlines() if line.startswith('data:'))
        if data:
            value=json.loads(data)
            if 'result' in value or 'error' in value: return value
    raise RuntimeError('MCP server did not return a JSON-RPC result.')

class Mcp:
    def __init__(self,endpoint=ENDPOINT):
        if endpoint!=ENDPOINT: raise ValueError('Only the configured official PlaySolana MCP endpoint is allowed.')
        self.endpoint=endpoint;self.token=token_from_environment();self.session=None;self.sequence=0;self.version='2025-03-26'
        initialized=self.rpc('initialize',{'protocolVersion':self.version,'capabilities':{},'clientInfo':{'name':'battlecities-build-launcher','version':'1.0.0'}})
        self.version=initialized.get('protocolVersion',self.version)
        self.rpc('notifications/initialized',{},notification=True)
    def rpc(self,method,params,notification=False):
        self.sequence+=1;body={'jsonrpc':'2.0','method':method,'params':params}
        if not notification: body['id']=self.sequence
        headers={'Authorization':'Bearer '+self.token,'Content-Type':'application/json','Accept':'application/json, text/event-stream','MCP-Protocol-Version':self.version}
        if self.session: headers['Mcp-Session-Id']=self.session
        request=urllib.request.Request(self.endpoint,json.dumps(body).encode(),headers,method='POST')
        # Do not follow redirects carrying the bearer credential.
        try:
            with network.urlopen(request,timeout=180) as response:
                self.session=response.headers.get('Mcp-Session-Id',self.session)
                decoded=decode_rpc(response.read())
        except urllib.error.HTTPError as e:
            raise RuntimeError(f'PlaySolana MCP returned HTTP {e.code}; check the token/account connection.') from None
        if notification: return None
        if not decoded: raise RuntimeError('Empty MCP response')
        if 'error' in decoded: raise RuntimeError('MCP request rejected: '+str(decoded['error'].get('message','Unknown error')))
        return decoded['result']
    def call(self,name,args):
        result=self.rpc('tools/call',{'name':name,'arguments':args})
        if result.get('isError'):
            # Keep signed upload URLs and any provider error details out of console logs.
            raise RuntimeError(f'PlaySolana {name} failed. Check publisher agreement, verified wallet and draft status in PlayGate.')
        value=result.get('structuredContent')
        if value is None:
            for block in result.get('content',[]):
                if block.get('type')=='text':
                    try: value=json.loads(block['text']);break
                    except (ValueError,KeyError): pass
        if not isinstance(value,dict): raise RuntimeError(f'No structured result from {name}')
        if isinstance(value.get('data'),dict): value=value['data']
        return value

def current_draft(client,submission):
    data=client.call('get_submission',{'id':submission})
    draft=data.get('submission')
    if not draft: raise RuntimeError('PSG1 submission not found')
    if draft.get('status')!='DRAFT': raise RuntimeError('PSG1 upload requires an editable DRAFT; open/create a draft in PlayGate first.')
    if not draft.get('authorityWalletVerified'): raise RuntimeError('Verify the draft authority wallet in PlayGate before uploading builds.')
    return draft

def parse_upload_form(command):
    """Parse data fields, never execute the server-provided shell command."""
    tokens=shlex.split(command);fields={};i=0
    while i<len(tokens):
        word=tokens[i];value=None
        if word in ('-F','--form','--form-string'):
            i+=1
            if i>=len(tokens): raise ValueError('Incomplete upload form')
            value=tokens[i]
        elif word.startswith('-F') and len(word)>2: value=word[2:]
        elif word.startswith('--form='): value=word.split('=',1)[1]
        if value is not None:
            name,sep,content=value.partition('=')
            if not sep or not re.fullmatch(r'[A-Za-z0-9_.-]+',name): raise ValueError('Unexpected upload form field')
            if name!='file':
                if content.startswith(('@','<')): raise ValueError('Upload form may not read local files')
                fields[name]=content
        i+=1
    if 'key' not in fields or not any(k.lower()=='policy' for k in fields):
        raise ValueError('Expected a presigned POST policy and object key; server upload format may have changed.')
    return fields

def storage_url(url):
    u=urllib.parse.urlsplit(url)
    if u.scheme!='https' or u.username or u.password or u.port not in (None,443) or not u.hostname or not u.hostname.endswith('.digitaloceanspaces.com'):
        raise ValueError('Unexpected PlayGate storage URL')
    return u

def upload_file(ticket,apk):
    u=storage_url(ticket['uploadUrl']);storage_url(ticket['publicUrl'])
    if apk.stat().st_size>int(ticket['maxBytes']): raise ValueError('APK exceeds PlayGate upload limit')
    fields=parse_upload_form(ticket['curl'])
    if fields['key']!=ticket['key']: raise ValueError('Upload object key mismatch')
    boundary='BattleCities'+uuid.uuid4().hex
    prefix=b''
    for name,value in fields.items():
        prefix+=(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n').encode()
    prefix+=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{apk.name}"\r\nContent-Type: {ticket["contentType"]}\r\n\r\n').encode()
    suffix=f'\r\n--{boundary}--\r\n'.encode()
    conn=network.HTTPSConnection(u.hostname,timeout=180,context=ssl.create_default_context())
    try:
        conn.putrequest('POST',urllib.parse.urlunsplit(('', '',u.path or '/',u.query,'')))
        conn.putheader('Content-Type','multipart/form-data; boundary='+boundary)
        conn.putheader('Content-Length',str(len(prefix)+apk.stat().st_size+len(suffix)))
        conn.endheaders();conn.send(prefix)
        with apk.open('rb') as f:
            while chunk:=f.read(1024*1024): conn.send(chunk)
        conn.send(suffix);response=conn.getresponse();response.read()
        if not 200<=response.status<300: raise RuntimeError(f'PlayGate storage rejected upload: HTTP {response.status}')
    finally: conn.close()
    # Independently verify all publicly hosted APK bytes before attaching the build.
    h=hashlib.sha256();count=0
    with network.urlopen(ticket['publicUrl'],timeout=180) as response:
        if response.url!=ticket['publicUrl']: raise RuntimeError('Unexpected CDN redirect')
        while chunk:=response.read(1024*1024): h.update(chunk);count+=len(chunk)
    with apk.open('rb') as f: local=hashlib.file_digest(f,'sha256').hexdigest()
    if count!=apk.stat().st_size or h.hexdigest()!=local: raise RuntimeError('Hosted APK checksum mismatch')
    return local

def main():
    p=argparse.ArgumentParser();p.add_argument('action',choices=['check','upload']);p.add_argument('--endpoint',default=ENDPOINT);p.add_argument('--submission',required=True)
    p.add_argument('--apk');p.add_argument('--receipt');p.add_argument('--output');a=p.parse_args()
    client=Mcp(a.endpoint);draft=current_draft(client,a.submission)
    if a.action=='check':
        print('PSG1 connected: editable draft and verified authority wallet. No build uploaded.');return
    if not all((a.apk,a.receipt,a.output)): p.error('upload requires --apk, --receipt and --output')
    apk=Path(a.apk).resolve();receipt=json.loads(Path(a.receipt).read_text(encoding='utf-8-sig'))
    if receipt.get('package')!='sol.battle.cities' or receipt.get('architecture')!='arm64-v8a' or not all(receipt.get(k) for k in ['apksignerVerified','zipalignVerified','previousCertificateMatches']):
        raise ValueError('A verified signed Battle Cities ARM64 APK is required')
    with apk.open('rb') as f: sha=hashlib.file_digest(f,'sha256').hexdigest()
    if sha!=receipt.get('apkSha256'): raise ValueError('APK no longer matches its verification receipt')
    filename=f'battlecities-{receipt["versionName"]}-{receipt["versionCode"]}.apk'
    ticket=client.call('get_upload_url',{'filename':filename,'contentType':'application/vnd.android.package-archive','kind':'build','submissionId':draft['id']})
    upload_file(ticket,apk)
    attached=client.call('manage_submission',{'action':'update','id':draft['id'],'fields':{'buildUrl':{filename:ticket['publicUrl']}}})
    analysis=attached.get('buildAnalysis',[])
    match=[row for row in analysis if row.get('name')==filename]
    if len(match)!=1 or not match[0].get('ok'): raise RuntimeError('APK uploaded, but PlayGate analysis did not confirm success. Review the dashboard before proceeding.')
    output={'publicUrl':ticket['publicUrl'],'submissionId':attached.get('submission',{}).get('id',draft['id']),'filename':filename,'sha256':sha,'analysisPassed':True,'submittedForReview':False}
    Path(a.output).write_text(json.dumps(output,indent=2))
    print('PSG1 draft updated; APK analysis passed. Not submitted for review.\n'+ticket['publicUrl'])

if __name__=='__main__':
    try: main()
    except Exception as e:
        # No traceback or signed URL/header dump; show actionable, sanitized errors.
        print('PSG1: '+str(e) if isinstance(e,(ValueError,RuntimeError)) else 'PSG1 request failed. Check network connectivity and the PlayGate account.',file=sys.stderr)
        sys.exit(1)
