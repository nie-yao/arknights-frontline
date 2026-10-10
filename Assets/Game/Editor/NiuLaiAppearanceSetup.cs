using UnityEditor;
using UnityEngine;
using ArknightsFrontline.Skills;
namespace ArknightsFrontline.Editor
{
    public static class NiuLaiAppearanceSetup
    {
        public static void Prepare()
        {
            AssetDatabase.Refresh();
            const string directory="Assets/Game/Characters/NiuLai/Resources/";
            var material=AssetDatabase.LoadAssetAtPath<Material>(directory+"NiuLaiMama.mat");
            if(!material){material=new Material(Shader.Find("ArknightsFrontline/NiuLaiMama"));AssetDatabase.CreateAsset(material,directory+"NiuLaiMama.mat");}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(directory+"texture_pbr_20250901.png"));
            material.SetColor("_SkinColor",new Color(1,0.29f,0.075f));EditorUtility.SetDirty(material);
            var empower=AssetDatabase.LoadAssetAtPath<Material>(directory+"NiuLaiEmpowered.mat");
            if(!empower){empower=new Material(AssetDatabase.LoadAssetAtPath<Material>(directory+"NiuLai.mat"));AssetDatabase.CreateAsset(empower,directory+"NiuLaiEmpowered.mat");}
            empower.EnableKeyword("_EMISSION"); empower.SetColor("_EmissionColor",new Color(0.9f,0.42f,0.03f));EditorUtility.SetDirty(empower);
            var root=PrefabUtility.LoadPrefabContents(directory+"NiuLaiPlayer.prefab");
            if(!root.GetComponent<NiuLaiEmpowerVisual>())root.AddComponent<NiuLaiEmpowerVisual>();
            PrefabUtility.SaveAsPrefabAsset(root,directory+"NiuLaiPlayer.prefab");PrefabUtility.UnloadPrefabContents(root);AssetDatabase.SaveAssets();
        }
    }
}
