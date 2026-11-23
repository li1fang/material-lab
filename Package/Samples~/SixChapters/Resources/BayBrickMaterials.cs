using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaterialDestruction.Campus
{
    internal static class BayBrickMaterials
    {
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        public static Material ForStyle(string id)
        {
            if (id != "clay" && id != "kiln" && id != "lime")
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown brick face.");
            if (materials.TryGetValue(id, out var existing) && existing)
                return existing;

            var shader = Resources.Load<Shader>("BayBrickPaint");
            var texture = Resources.Load<Texture2D>("BayBrickTextures/" + id);
            if (!shader || !shader.isSupported || !texture)
                throw new InvalidOperationException("Missing brick face texture or shader: " + id);
            var material = new Material(shader) { name = "BayBrick-" + id };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_TileScale", 2f);
            materials[id] = material;
            return material;
        }

        public static void ReleaseAll()
        {
            foreach (var m in materials.Values)
                if (m) UnityEngine.Object.Destroy(m);
            materials.Clear();
        }
    }
}
