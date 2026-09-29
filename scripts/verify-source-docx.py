"""本机私有原稿与导出文件的独立核对，不把正文写入控制台。"""
import argparse,json,re
from pathlib import Path
from docx import Document
from docx.oxml.ns import qn
from docx.table import Table
from docx.text.paragraph import Paragraph

def verify(private,sources):
 layouts=json.loads((private/'source-layouts.json').read_text(encoding='utf-8'))
 audits=json.loads((private/'source-fidelity-audit.json').read_text(encoding='utf-8'))
 exports=json.loads((private/'source-export-tests.json').read_text(encoding='utf-8'))
 for layout,audit,export in zip(layouts,audits,exports,strict=True):
  doc=Document(sources/layout['sourceFile'])
  originals={i:node for i,node in enumerate(doc.element.body)}
  evidence={(p['sourcePara'],p['position']):p for p in audit['positions']}
  expected=[]
  for i,node in originals.items():
   if node.tag==qn('w:p') and Paragraph(node,doc).text.strip(): expected.append((i,'paragraph'))
   elif node.tag==qn('w:tbl'): expected.append((i,'table'))
  represented=[(expected[0][0],'paragraph')]
  for section in layout['sections']:
   if not section['id'].endswith('intro'):
    assert Paragraph(originals[section['sourcePara']],doc).text.strip()==section['title']
    represented.append((section['sourcePara'],'paragraph'))
   for block in section['blocks']:
    i=block['sourcePara'];represented.append((i,block['kind']))
    cells=[('p',block['template'],Paragraph(originals[i],doc).text.strip())] if block['kind']=='paragraph' else [(f'r{r}c{c}',value,Table(originals[i],doc).rows[r].cells[c].text.strip()) for r,row in enumerate(block['rows']) for c,value in enumerate(row)]
    for key,template,original in cells:
     values=evidence[(i,key)]['oldValues']
     assert re.sub(r'\{([a-z][a-z0-9_]*)\}',lambda m:values[m[1]],template)==original
     if 'project_location' in values: assert not values['project_location'].startswith('同一连接区段')
  assert represented==expected,layout['templateId']
  output=Document(export['output'])
  blocks=[n for n in output.element.body if n.tag!=qn('w:sectPr')]
  expected_output=[('paragraph',export['request']['document']['title'])]
  for section in export['request']['document']['sections']:
   expected_output.append(('paragraph',section['title']))
   for block in section['blocks']:expected_output.append((block['kind'],block.get('text',block.get('rows'))))
  assert len(blocks)==len(expected_output)
  for node,(kind,value) in zip(blocks,expected_output,strict=True):
   if kind=='paragraph': assert Paragraph(node,output).text==value
   else:
    table=Table(node,output)
    assert [[c.text for c in row.cells] for row in table.rows]==value
    widths=[int(c.get(qn('w:w'))) for c in node.xpath('./w:tblGrid/w:gridCol')]
    assert sum(widths)==9638
    assert len(node.xpath('./w:tr[1]/w:trPr/w:tblHeader'))==1
    assert all(row[0].tag==qn('w:trPr') for row in node.xpath('./w:tr'))
    assert all(cell[0].tag==qn('w:tcPr') for cell in node.xpath('./w:tr/w:tc'))
 print('SOURCE_DOCX_EXACT_OK templates=7 paragraphs=1355 tables=29')

if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--private',type=Path,required=True);parser.add_argument('--sources',type=Path,required=True)
 args=parser.parse_args();verify(args.private,args.sources)
