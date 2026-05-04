using UnityEngine;
using System.Collections.Generic;

public class GhostEffect : MonoBehaviour
{
    public float fadeSpeed = 3f; 
    private float alpha = 0.4f;
    private bool initialized = false;

    private List<Mesh> bakedMeshes = new List<Mesh>();
    
    // 🟢 HỆ THỐNG TRUE POOLING: Quản lý các bộ phận cơ thể để tái sử dụng
    private List<MeshFilter> meshFilters = new List<MeshFilter>();
    private List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
    private int activeMeshCount = 0;

    void OnEnable()
    {
        alpha = 0.4f;
        initialized = false;
        activeMeshCount = 0;
        
        // Chỉ dọn dẹp Mesh Data cũ, KHÔNG Destroy GameObject nữa
        foreach (var mesh in bakedMeshes) 
        {
            if (mesh != null) Destroy(mesh); 
        }
        bakedMeshes.Clear();
    }

    public void Setup(Renderer[] sourceRenderers, Material targetMaterial)
    {
        foreach (var source in sourceRenderers)
        {
            if (!source.gameObject.activeInHierarchy || !source.enabled) continue;

            MeshFilter mf;
            MeshRenderer mr;

            // 🟢 TÁI SỬ DỤNG BỘ PHẬN CŨ NẾU CÓ
            if (activeMeshCount < meshFilters.Count)
            {
                mf = meshFilters[activeMeshCount];
                mr = meshRenderers[activeMeshCount];
                mf.gameObject.SetActive(true);
            }
            // 🟢 NẾU THIẾU THÌ MỚI ĐẺ THÊM
            else
            {
                GameObject child = new GameObject("GhostMeshPart");
                child.transform.SetParent(this.transform);
                mf = child.AddComponent<MeshFilter>();
                mr = child.AddComponent<MeshRenderer>();
                
                meshFilters.Add(mf);
                meshRenderers.Add(mr);
            }

            // Ép vị trí, góc xoay và kích thước khớp 100% với bản thể
            mf.transform.position = source.transform.position;
            mf.transform.rotation = source.transform.rotation;
            mf.transform.localScale = source.transform.lossyScale;

            // Cập nhật Material
            Material[] ghostMats = new Material[source.sharedMaterials.Length];
            for (int i = 0; i < ghostMats.Length; i++) ghostMats[i] = targetMaterial;
            mr.materials = ghostMats;

            // Trích xuất hình dáng (Pose) hiện tại
            if (source is SkinnedMeshRenderer smr)
            {
                Mesh bakedMesh = new Mesh();
                smr.BakeMesh(bakedMesh);
                bakedMeshes.Add(bakedMesh);
                mf.sharedMesh = bakedMesh;
                activeMeshCount++;
            }
            else if (source is MeshRenderer sourceMr)
            {
                MeshFilter sourceMf = source.GetComponent<MeshFilter>();
                if (sourceMf != null)
                {
                    mf.sharedMesh = sourceMf.sharedMesh;
                    activeMeshCount++;
                }
            }
        }

        // Tắt đi những bộ phận thừa (nếu có)
        for (int i = activeMeshCount; i < meshFilters.Count; i++)
        {
            meshFilters[i].gameObject.SetActive(false);
        }

        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;
        
        alpha -= fadeSpeed * Time.deltaTime;
        
        for (int i = 0; i < activeMeshCount; i++)
        {
            if (meshRenderers[i].material.HasProperty("_FadeAlpha"))
            {
                meshRenderers[i].material.SetFloat("_FadeAlpha", alpha);
            }
        }

        if (alpha <= 0) 
        {
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.ReturnToPool(gameObject);
            else
                Destroy(gameObject);
        }
    }
}