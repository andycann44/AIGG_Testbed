using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG
{
    internal static class TempMerge
    {
        public static string LatestBatchDir
            => Path.Combine(AISeparator.Root, "Batches", DateTime.Now.ToString("yyyyMMdd_HHmmss"));

        /// <summary>
        /// Call this from Pre-Merge when AI returns the single JSON reply.
        /// </summary>
        public static void SaveFromAI(string json)
        {
            Directory.CreateDirectory(AISeparator.Root);
            var path = AISeparator.AiOut;
            File.WriteAllText(path, json ?? "");
            Debug.Log($"[TempMerge] Wrote AI output to: {path}");
            AISeparator.SplitAndOpen(path);
        }
    }
}
