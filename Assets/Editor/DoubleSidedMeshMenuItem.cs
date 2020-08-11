﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class DoubleSidedMeshMenuItem
{
    [MenuItem("Assets/Create/Double-Sided Mesh")]
    static void MakeDoubleSidedMeshAsset()
    {
        var sourceMesh = Selection.activeObject as Mesh;
        if (sourceMesh == null)
        {
            Debug.Log("请选择Mesh对象再进行操作");
            return;
        }

        Mesh insideMesh = Object.Instantiate(sourceMesh);
        int[] triangles = insideMesh.triangles;
        System.Array.Reverse(triangles);

        Vector3[] normals = insideMesh.normals;
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = -normals[i];
        }
        insideMesh.normals = normals;

        var combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(
            new CombineInstance[] 
            {
                new CombineInstance { mesh=insideMesh},
                new CombineInstance { mesh=sourceMesh}
             }, 
            true, false, false);

        Object.DestroyImmediate(insideMesh);
        AssetDatabase.CreateAsset(combinedMesh, 
            System.IO.Path.Combine("Assets", sourceMesh.name + " Double-Sided.asset")
            );
    }
}
