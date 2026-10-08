using BepuPhysics;

namespace Shared.Collision.ZoneLoading;

/// <summary>
///     What one chunk contributes to the server's picture of the world.
/// </summary>
/// <param name="Statics">The static geometry: ground and scenery, which stop shots and creatures alike.</param>
/// <param name="Blockers">
///     The movement-blocker layer: invisible shapes that stop movement and nothing else. Their shapes live
///     in the separate simulation passed for them, so no world query can meet one by accident.
/// </param>
/// <param name="WaterMaterialIds">
///     The physics materials of the chunk's water collision. Each names a <c>dbvisualrecords::WaterDesc</c>
///     row through its <c>physics_material_id</c>, so between them they say which waters the chunk holds.
/// </param>
public sealed record ChunkCollision(StaticDescription[] Statics, StaticDescription[] Blockers, uint[] WaterMaterialIds)
{
    public static readonly ChunkCollision Empty = new([], [], []);
}
