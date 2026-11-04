import subprocess, os, re, glob, sys
LABELS=[('energia',r'valor\s*energ'),('carbo',r'carboidrat'),('acucares',r'a[cç].?[uú]car.*tota'),
('proteinas',r'prote[ií]'),('gtotais',r'gordura.*tota'),('gsat',r'gordura.*satur'),
('gtrans',r'gordura.*trans'),('fibras',r'fibra'),('sodio',r's[oó]dio')]
def firstnum(s):
    s=re.sub(r'\([^)]*\)',' ',s)
    m=re.search(r'(\d+(?:[.,]\d+)?)',s); return m.group(1).replace(',','.') if m else ''
def parse(t):
    v={}
    for k,p in LABELS:
        v[k]=''
        for ln in t.split('\n'):
            mm=re.search(p,ln,re.I)
            if mm: v[k]=firstnum(ln[mm.end():]); break
    mp=re.search(r'por[cç][aã]o[:\s]*([0-9]+\s*g[^\n|]*)',t,re.I)
    return (mp.group(1).strip() if mp else '100 g')[:38], v
pdfs=sorted(glob.glob('*.pdf')); tot=len(pdfs)
for i,pdf in enumerate(pdfs,1):
    b=pdf[:-4]
    subprocess.run(['pdftoppm','-r','300','-png',pdf,b+'_pg'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    t=''
    for png in sorted(glob.glob(b+'_pg*.png')):
        r=subprocess.run(['tesseract',png,'stdout','-l','por','--psm','6'],capture_output=True,text=True)
        t+=r.stdout+'\n'; os.remove(png)
    porcao,v=parse(t)
    print('|'.join([b,porcao,v['energia'],v['carbo'],v['acucares'],v['proteinas'],v['gtotais'],v['gsat'],v['gtrans'],v['fibras'],v['sodio']]),flush=True)
    sys.stderr.write(f'{i}/{tot} {b}\n'); sys.stderr.flush()
