using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
[GenerateAuthoringComponent]
public struct RotationConstraints : IComponentData
{
    public bool x;
    public bool y;
    public bool z;
}
