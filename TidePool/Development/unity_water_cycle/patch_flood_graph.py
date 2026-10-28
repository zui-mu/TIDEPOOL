import copy,json,pathlib,uuid
root=pathlib.Path(__file__).parent
file=root/'SG_WetTiles_WaterCycle.shadergraph'
source=file.read_text(encoding='utf-8-sig'); decoder=json.JSONDecoder();objects=[];i=0
while i<len(source):
    while i<len(source) and source[i].isspace(): i+=1
    if i==len(source):break
    obj,i=decoder.raw_decode(source,i);objects.append(obj)
by={o['m_ObjectId']:o for o in objects};graph=objects[0]
def uid():return uuid.uuid4().hex
def edge(a,sa,b,sb):return {'m_OutputSlot':{'m_Node':{'m_Id':a},'m_SlotId':sa},'m_InputSlot':{'m_Node':{'m_Id':b},'m_SlotId':sb}}
def clone_node(original,x,y):
    n=copy.deepcopy(by[original]);n['m_ObjectId']=uid();n['m_DrawState']['m_Position'].update(x=x,y=y);n['m_PreviewExpanded']=False
    slots={}
    for r in n['m_Slots']:
        s=copy.deepcopy(by[r['m_Id']]);s['m_ObjectId']=uid();r['m_Id']=s['m_ObjectId'];objects.append(s);slots[s['m_Id']]=s
    objects.append(n);graph['m_Nodes'].append({'m_Id':n['m_ObjectId']});return n,slots
if any(o.get('m_DefaultReferenceName')=='_FloodWetness' for o in objects):
    print('Already patched');raise SystemExit
prop=copy.deepcopy(by['789398c81d5c453b9656b40a53f2b410']);prop['m_ObjectId']=uid();prop['m_Guid']['m_GuidSerialized']=str(uuid.uuid4())
prop['m_Name']=prop['m_RefNameGeneratedByDisplayName']='FloodWetness';prop['m_DefaultReferenceName']='_FloodWetness';prop['m_Value']=0
objects.append(prop);graph['m_Properties'].append({'m_Id':prop['m_ObjectId']})
for o in objects:
    if o.get('m_Type')=='UnityEditor.ShaderGraph.CategoryData':o['m_ChildObjectList'].append({'m_Id':prop['m_ObjectId']})
pn,_=clone_node('4346342304be42ed82ea9fb715bebfd0',-220,1960);pn['m_Property']['m_Id']=prop['m_ObjectId']
lerp,slots=clone_node('2e20a44db2d74bff8c97ef45858ffefc',40,1840)
for k in slots[1]['m_Value']:slots[1]['m_Value'][k]=1.0
# Locate the final multiply: it alone feeds both wet-colour/smoothness Lerp T inputs.
consumers={'2161f06660ec40a2b410758ea7f13b0b','2e20a44db2d74bff8c97ef45858ffefc'}
final={e['m_OutputSlot']['m_Node']['m_Id'] for e in graph['m_Edges'] if e['m_InputSlot']['m_Node']['m_Id'] in consumers and e['m_InputSlot']['m_SlotId']==2}
assert len(final)==1
node=next(iter(final))
incoming=next(e for e in graph['m_Edges'] if e['m_InputSlot']['m_Node']['m_Id']==node and e['m_InputSlot']['m_SlotId']==0)
old=incoming['m_OutputSlot'];incoming['m_OutputSlot']={'m_Node':{'m_Id':lerp['m_ObjectId']},'m_SlotId':3}
graph['m_Edges'].append(edge(old['m_Node']['m_Id'],old['m_SlotId'],lerp['m_ObjectId'],0))
graph['m_Edges'].append(edge(pn['m_ObjectId'],0,lerp['m_ObjectId'],2))
# This is BEFORE the dry-region multiply, so the air pocket remains truly dry.
file.write_text('\n\n'.join(json.dumps(o,indent=4) for o in objects)+'\n',encoding='utf-8')
print('Patched flood wetness before dry-region mask:',len(objects),'objects')
