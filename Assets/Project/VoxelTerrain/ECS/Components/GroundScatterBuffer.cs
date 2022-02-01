using Unity.Entities;

namespace VoxelTerrain.ECS.Components
{
    public struct GroundScatterBufferElement<Component> : IBufferElementData where Component : IGroundScatter
    {
        public Component component;

        public static implicit operator Component(GroundScatterBufferElement<Component> e) { return e.component; }
        public static implicit operator GroundScatterBufferElement<Component>(Component e) { return new GroundScatterBufferElement<Component> { component = e }; }
    }
}
