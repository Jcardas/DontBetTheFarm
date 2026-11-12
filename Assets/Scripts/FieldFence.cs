
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class FieldFence : MonoBehaviour
{
    public enum Side { MinX, MaxX, MinZ, MaxZ }

    public Renderer ground;
    public GameObject fencePrefab;

    public Side[] sides = { Side.MinX, Side.MinZ };

    public float segmentLength = 2.5f;
    public float inset = 0.2f;

    void Start()
    {
        Generate();
    }
    
   
[ContextMenu("Generate Fences")]
public void Generate()
{
    if (!ground || !fencePrefab) return;

    Bounds b = ground.bounds;

    foreach (Side side in sides)
    {
        bool alongX = side == Side.MinZ || side == Side.MaxZ;

        float fixedPos = side switch
        {
            Side.MinX => b.min.x + inset,
            Side.MaxX => b.max.x - inset,
            Side.MinZ => b.min.z + inset,
            _ => b.max.z - inset
        };

        Vector3 start = alongX
            ? new Vector3(b.min.x, b.max.y, fixedPos)
            : new Vector3(fixedPos, b.max.y, b.min.z);

        CreateFence(
            side == Side.MinX ? "Fence_BottomLeft" :
            side == Side.MinZ ? "Fence_BottomRight" :
            "Fence_" + side,
            start,
            alongX ? Vector3.right : Vector3.forward,
            alongX ? 0f : -90f,
            alongX ? b.size.x : b.size.z
        );
    }
}



    void CreateFence(string name, Vector3 start,
        Vector3 direction, float angle, float length)
    {
        float size = Mathf.Max(0.01f, segmentLength);
        int count = Mathf.Max(1, Mathf.CeilToInt(length / size));
        float step = length / count;

        var parts = fencePrefab.GetComponentsInChildren<MeshFilter>(true);
        var meshes = new List<CombineInstance>();

        for (int i = 0; i < count; i++)
        {
            Matrix4x4 placement = transform.worldToLocalMatrix *
                Matrix4x4.TRS(
                    start + direction * (i * step),
                    Quaternion.Euler(0, angle, 0),
                    new Vector3(step / size, 1, 1)
                );

            foreach (var part in parts)
            {
                if (!part.sharedMesh) continue;

                meshes.Add(new CombineInstance
                {
                    mesh = part.sharedMesh,
                    transform = placement *
                        fencePrefab.transform.worldToLocalMatrix *
                        part.transform.localToWorldMatrix
                });
            }
        }

        if (meshes.Count == 0) return;

        // Reuse existing fence object
        Transform existing = transform.Find(name);

        GameObject fence = existing
            ? existing.gameObject
            : new GameObject(name);

        if (!existing)
            fence.transform.SetParent(transform, false);

        MeshFilter filter = fence.GetComponent<MeshFilter>();

        if (!filter)
            filter = fence.AddComponent<MeshFilter>();

        Mesh mesh = filter.sharedMesh;

        if (!mesh || mesh.hideFlags != HideFlags.DontSave)
        {
            mesh = new Mesh { name = name };
            mesh.hideFlags = HideFlags.DontSave;
            filter.sharedMesh = mesh;
        }

        mesh.Clear();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.CombineMeshes(meshes.ToArray(), true, true);
        mesh.RecalculateBounds();

        MeshRenderer renderer = fence.GetComponent<MeshRenderer>();

        if (!renderer)
            renderer = fence.AddComponent<MeshRenderer>();

        MeshRenderer source =
            fencePrefab.GetComponentInChildren<MeshRenderer>(true);

        if (source)
            renderer.sharedMaterial = source.sharedMaterial;
    }
}
