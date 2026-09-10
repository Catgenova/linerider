using System.Collections.Generic;

namespace CyberRider.Core
{
    /// <summary>
    /// Anything that lives in the world besides static lines and riders: moving platforms, gears,
    /// fans, magnets, cannons, breakable walls, hazards. Entities may expose kinematic collision
    /// lines, apply forces to rider points, and react to contacts.
    /// </summary>
    public abstract class Entity
    {
        private static int _nextId = 1;

        public readonly int Id = _nextId++;
        public abstract string Kind { get; }
        /// <summary>Kinematic collision lines. Their Vx/Vy must be kept up to date in Update().</summary>
        public readonly List<Line> Lines = new List<Line>();
        public bool Active = true;
        /// <summary>The definition this entity was built from (renderers read its geometry).</summary>
        public EntityDef Def;

        /// <summary>Called once per frame before integration.</summary>
        public abstract void Update(World world);

        /// <summary>Add forces to rider points (into rider.Forces). Optional.</summary>
        public virtual void ApplyForces(World world)
        {
        }

        /// <summary>Called after the collision passes each frame. Optional.</summary>
        public virtual void AfterStep(World world)
        {
        }
    }
}
