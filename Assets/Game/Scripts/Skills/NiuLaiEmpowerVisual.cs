using UnityEngine;
using ArknightsFrontline.Combat;
namespace ArknightsFrontline.Skills
{
    public sealed class NiuLaiEmpowerVisual : MonoBehaviour
    {
        private NiuLaiSkillController skills;
        private GameObject aura;
        private LineRenderer ring;
        private LineRenderer[] sparks;
        private Material glow;
        private Material[] skins;
        private Renderer[] renderers;
        private Material[][] originals;
        public bool IsVisible => aura && aura.activeSelf;
        private void Awake()
        {
            skills=GetComponent<NiuLaiSkillController>();
            aura=new GameObject("EmpowerAura"); aura.transform.SetParent(transform,false);
            glow=new Material(Shader.Find("Universal Render Pipeline/Unlit")); glow.color=new Color(1,0.72f,0.08f);
            ring=CreateLine("EmpowerRing",65); ring.loop=true;
            sparks=new LineRenderer[6]; for(int i=0;i<sparks.Length;i++) sparks[i]=CreateLine("EmpowerSpark"+i,2);
            var visual=transform.Find("NiuLaiVisual"); renderers=visual.GetComponentsInChildren<Renderer>(true);
            skins=new Material[renderers.Length]; originals=new Material[renderers.Length][];
            for(int i=0;i<renderers.Length;i++)
            {
                originals[i]=renderers[i].sharedMaterials;
                skins[i]=new Material(Resources.Load<Material>("NiuLaiEmpowered") ?? renderers[i].sharedMaterial); skins[i].SetColor("_EmissionColor",Color.black);
                renderers[i].sharedMaterial=skins[i];
            }
            aura.SetActive(false);
        }
        private LineRenderer CreateLine(string name,int count)
        {
            var o=new GameObject(name); o.transform.SetParent(aura.transform,false); var line=o.AddComponent<LineRenderer>();
            line.positionCount=count; line.useWorldSpace=true; line.startWidth=line.endWidth=0.045f; line.sharedMaterial=glow; return line;
        }
        private void LateUpdate()=>RefreshVisual();
        public void RefreshVisual()
        {
            bool active=skills && skills.IsEmpowered && skills.CanShowIndicators;
            aura.SetActive(active);
            foreach(var skin in skins) skin.SetColor("_EmissionColor",active?new Color(0.9f,0.42f,0.03f)*(0.8f+0.2f*Mathf.Sin(Time.time*8)):Color.black);
            if(!active)return;
            Vector3 center=transform.position; center.y=0.09f;
            float radius=0.8f+0.06f*Mathf.Sin(Time.time*6);
            for(int i=0;i<65;i++){float angle=i*Mathf.PI*2/64;ring.SetPosition(i,center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius);}
            for(int i=0;i<sparks.Length;i++)
            {
                float angle=Time.time*2+i*Mathf.PI*2/sparks.Length;
                Vector3 point=center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*0.72f;
                point.y+=Mathf.Repeat(Time.time*1.3f+i*0.17f,1)*1.5f;
                sparks[i].SetPosition(0,point);sparks[i].SetPosition(1,point+Vector3.up*0.25f);
            }
        }
        private void OnDisable(){if(aura)aura.SetActive(false);if(skins!=null)foreach(var skin in skins)if(skin)skin.SetColor("_EmissionColor",Color.black);}
        private void OnDestroy()
        {
            if(renderers!=null)for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].sharedMaterials=originals[i];
            if(skins!=null)foreach(var skin in skins)if(skin)Destroy(skin);
            if(glow)Destroy(glow);
        }
    }
}
