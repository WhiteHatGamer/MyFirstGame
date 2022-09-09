// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using System.Collections.Generic;
using UnityEngine;

namespace LEGOModelImporter
{
    public class Model : MonoBehaviour
    {
        public enum Pivot
        {
            Original,
            Center,
            BottomCenter
        };

        public string absoluteFilePath;
        public string relativeFilePath;
        public Pivot pivot;

        public List<ModelGroup> groups = new List<ModelGroup>();
    }
}