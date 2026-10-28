import copy, json, pathlib, uuid
root = pathlib.Path(__file__).parent
objects = json.loads((root/'graph_objects.json').read_text())
by = {o['m_ObjectId']:o for o in objects}
graph = objects[0]
def uid(): return uuid.uuid4().hex
def clone_node(old, x, y, kind=None):
    n=copy.deepcopy(by[old]); n['m_ObjectId']=uid()
    if kind:
        n['m_Type']='UnityEditor.ShaderGraph.'+kind+'Node';n['m_Name']=kind;n['synonyms']=[]
    n['m_DrawState']['m_Position'].update(x=x,y=y)
    n['m_PreviewExpanded']=False
    slots={}
    for ref in n['m_Slots']:
        s=copy.deepcopy(by[ref['m_Id']]); s['m_ObjectId']=uid();ref['m_Id']=s['m_ObjectId']
        objects.append(s);by[s['m_ObjectId']]=s;slots[s['m_Id']]=s
    objects.append(n);by[n['m_ObjectId']]=n;graph['m_Nodes'].append({'m_Id':n['m_ObjectId']})
    return n['m_ObjectId'],slots
def edge(a,sa,b,sb): graph['m_Edges'].append({'m_OutputSlot':{'m_Node':{'m_Id':a},'m_SlotId':sa},'m_InputSlot':{'m_Node':{'m_Id':b},'m_SlotId':sb}})
def scalar(slot,value):
    if isinstance(slot['m_Value'],dict):
        for k in slot['m_Value']:slot['m_Value'][k]=value
    else:slot['m_Value']=value
def vec(x,y,px,py):
    n,s=clone_node('0d5d5873e01e4ac3b17903994cd3c8b0',px,py)
    s[1]['m_Value']=x;s[2]['m_Value']=y;by[n]['m_Value']={'x':x,'y':y};return n
# Always-wet baseline, with low frequency mottling. Preserve the larger pool-edge mask.
prop=copy.deepcopy(by['789398c81d5c453b9656b40a53f2b410']);prop['m_ObjectId']=uid();prop['m_Guid']['m_GuidSerialized']=str(uuid.uuid4())
for k in ['m_Name','m_RefNameGeneratedByDisplayName']:prop[k]='BaseWetness'
prop['m_DefaultReferenceName']='_BaseWetness';prop['m_Value']=.48
objects.append(prop);graph['m_Properties'].append({'m_Id':prop['m_ObjectId']})
for o in objects:
    if o.get('m_Type')=='UnityEditor.ShaderGraph.CategoryData':o['m_ChildObjectList'].append({'m_Id':prop['m_ObjectId']})
p,ps=clone_node('4346342304be42ed82ea9fb715bebfd0',-700,1740);by[p]['m_Property']['m_Id']=prop['m_ObjectId']
noise,ns=clone_node('0e6f1ffc6d7d40ee8f2f529807f3719c',-960,1550);scalar(ns[1],.18)
edge('56c80e16bd944f538e50d75ad8b924cb',2,noise,0)
base,bs=clone_node('f737eb605bec4721a9d5a07faa0a6638',-470,1570,'Add');edge(noise,2,base,0);edge(p,0,base,1)
maximum,ms=clone_node('f737eb605bec4721a9d5a07faa0a6638',-220,1570)
edge(base,2,maximum,0);edge('a6c8f4464f6a4554aaf236426e1413e3',2,maximum,1)
# This rectangle matches the authored 4 x 4 metre exclusion at (8,39.5).
center=vec(8,39.5,-1900,2050);size=vec(4,4,-1660,2240);half=vec(.5,.5,-1420,2410)
sub,ss=clone_node('1943639fdf6c49c78b5d0aabd52decd8',-1660,2030)
edge('6a544dcbe7f5450e85351346932ba304',6,sub,0);edge(center,0,sub,1)
div,ds=clone_node('c8915d5880bc418096450e8637b469e0',-1420,2040);edge(sub,2,div,0);edge(size,0,div,1)
add,ads=clone_node('f737eb605bec4721a9d5a07faa0a6638',-1180,2050,'Add');edge(div,2,add,0);edge(half,0,add,1)
rect,rs=clone_node('5b98774fb77d4dedba95ae6c6c43fe54',-940,2050);rs[1]['m_Value']=1.;rs[2]['m_Value']=1.;edge(add,2,rect,0)
outside,os=clone_node('643ef3e4fcfc4c109c410cab59f3c5ab',-700,2050);edge(rect,3,outside,0)
final,fs=clone_node('0e6f1ffc6d7d40ee8f2f529807f3719c',30,1580)
# Replace only the two wet-mask consumers. Existing edge-mask math remains readable.
for e in graph['m_Edges']:
    if e['m_OutputSlot']['m_Node']['m_Id']=='a6c8f4464f6a4554aaf236426e1413e3' and e['m_InputSlot']['m_Node']['m_Id'] in ['2161f06660ec40a2b410758ea7f13b0b','2e20a44db2d74bff8c97ef45858ffefc']:
        e['m_OutputSlot']['m_Node']['m_Id']=final
edge(maximum,2,final,0);edge(outside,1,final,1)
# Base / normal / lightmap output remains the learned URP Lit graph.
out=root/'SG_WetTiles_WaterCycle.shadergraph'
out.write_text('\n\n'.join(json.dumps(o,indent=4) for o in objects)+'\n',encoding='utf-8')
ids=[o['m_ObjectId'] for o in objects];assert len(ids)==len(set(ids))
assert all(e['m_InputSlot']['m_Node']['m_Id'] in ids and e['m_OutputSlot']['m_Node']['m_Id'] in ids for e in graph['m_Edges'])
print(f'{out}: {len(objects)} objects, {len(graph["m_Edges"])} connections')
