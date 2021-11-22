namespace SimpleECS
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Internal;


    /// <summary>
    /// manages all entities and archetype information
    /// </summary>
    public partial struct World : IEquatable<World>, IEnumerable<Archetype>
    {
        World(int index, int version)
        {
            this.index = index; this.version = version;
        }

        /// <summary>
        /// the index and version together create a unique identifier for the world
        /// </summary>
        public readonly int index;

        /// <summary>
        /// the index and version together create a unique identifier for the world
        /// </summary>
        public readonly int version;

        /// <summary>
        /// Name of the world
        /// </summary>
        public string Name
        {
            get => this.TryGetWorldInfo(out var info) ? info.name : "~World";
            set
            {
                if (this.TryGetWorldInfo(out var info))
                    info.name = value;
            }
        }

        /// <summary>
        /// Returns a copy of all the archetypes in the current world
        /// </summary>
        public Archetype[] GetArchetypes()
        {
            Archetype[] archetypes;
            if (this.TryGetWorldInfo(out var info))
            {
                archetypes = new Archetype[info.archetype_count];
                int count = 0;
                foreach (var arch in info.archetypes)
                    if (arch.data != null)
                    {
                        archetypes[count] = arch.data.archetype;
                        count++;
                    }
            }
            else
                archetypes = new Archetype[0];
            return archetypes;
        }

        /// <summary>
        /// Returns a copy of all the entities in the the current world
        /// </summary>
        public Entity[] GetEntities()
        {
            Entity[] entities;
            if (this.TryGetWorldInfo(out var info))
            {
                entities = new Entity[EntityCount];
                int count = 0;
                foreach (var archetype in info.archetypes)
                {
                    if (archetype.data != null)
                        for (int e = 0; e < archetype.data.entity_count; ++e)
                        {
                            entities[count] = archetype.data.entities[e];
                            count++;
                        }
                }
            }
            else entities = new Entity[0];
            return entities;
        }

        /// <summary>
        /// Creates a new world
        /// </summary>
        public static World Create() => Create("World");

        /// <summary>
        /// Gets world with name, else creates and returns a world with name
        /// </summary>
        public static World GetOrCreate(string name)
        {
            if (!TryGetWorld(name, out var world))
                return Create(name);
            return world;
        }

        /// <summary>
        /// Tries to get world with name
        /// </summary>
        /// <param name="name">name of the target world</param>
        /// <param name="target_world">target world</param>
        /// <returns>returns false if not found</returns>
        public static bool TryGetWorld(string name, out World target_world)
        {
            foreach (var current in GetAll())
                if (current.Name == name)
                {
                    target_world = current;
                    return true;
                }
            target_world = default;
            return false;
        }

        /// <summary>
        /// Creates an new world with Name
        /// </summary>
        public static World Create(string Name)
        {
            var index = -1;
            for (int i = 0; i < World_Info.All.Length; ++i)
            {
                if (World_Info.All[i].data == null)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
            {
                index = World_Info.All.Length;
                System.Array.Resize(ref World_Info.All, index + 4);
            }

            ref var world_data = ref World_Info.All[index];
            var version = world_data.version;
            world_data.data = new World_Info(Name, new World(index, version));
            World_Info.world_count++;
            return world_data.data.world;
        }

        /// <summary>
        /// Returns true if the world is not null or destroyed
        /// </summary>
        public bool IsValid() => World_Info.All[index].version == version;

        /// <summary>
        /// Destroys the world along with all it's archetypes and entities
        /// </summary>
        public void Destroy()
        {
            if (this.TryGetWorldInfo(out var info))
                info.StructureEvents.DestroyWorld();
        }

        /// <summary>
        /// Creates an entity in this world
        /// </summary>
        public Entity CreateEntity()
        {
            if (this.TryGetWorldInfo(out var info))
                return info.StructureEvents.CreateEntity(info.GetArchetypeData(info.buffer_signature.Clear()));
            return default;
        }

        /// <summary>
        /// Returns how many entities are currently in this world
        /// </summary>
        public int EntityCount => this.TryGetWorldInfo(out var info) ? info.entity_count : 0;

        /// <summary>
        /// Creates a query that operates on this world
        /// </summary>
        public Query CreateQuery() => new Query(this);

        /// <summary>
        /// Tries to get the archetype that matches the supplied TypeSignature.
        /// Returns false if the world is destroyed or null
        /// </summary>
        public bool TryGetArchetype(out Archetype archetype, TypeSignature signature)
        {
            if (this.TryGetWorldInfo(out var info))
            {
                archetype = info.GetArchetypeData(signature).archetype;
                return true;
            }
            archetype = default;
            return false;
        }

        /// <summary>
        /// Tries to get an archetype that has the supplied types.
        /// Returns false if the world is destroyed or null
        /// </summary>
        public bool TryGetArchetype(out Archetype archetype, params Type[] types)
            => TryGetArchetype(out archetype, new TypeSignature(types));

        /// <summary>
        /// WorldData is data unique to this world
        /// Set's the world's data to value.
        /// </summary>
        public World SetData<WorldData>(WorldData world_data)
        {
            if (this.TryGetWorldInfo(out var info))
                info.GetData<WorldData>().data = world_data;
            return this;
        }

        /// <summary>
        /// WorldData is data unique to this world
        /// Get's a reference to the data which can be assigned.
        /// Throws an exception if the world is destroyed or null
        /// </summary>
        public ref WorldData GetData<WorldData>()
        {
            if (this.TryGetWorldInfo(out var info))
                return ref info.GetData<WorldData>().data;
            throw new Exception($"{this} is invalid, cannot get resource {typeof(WorldData).Name}");
        }

        /// <summary>
        /// Adds a callback to be invoked whenever an entity sets a component of type
        /// </summary>
        /// <param name="callback">callback to invoke</param>
        /// <param name="register">set true to add callback, false to remove the callback</param>
        public World OnSet<Component>(SetComponentEvent<Component> callback, bool register = true)
        {
            if (this.TryGetWorldInfo(out var info))
            {
                var data = info.GetData<Component>();
                if (register)
                    data.set_callback += callback;
                else data.set_callback -= callback;
                data.has_set_callback = data.set_callback != null;
            }
            return this;
        }

        /// <summary>
        /// Adds a callback to be invoked whenever an entity sets a component of type
        /// </summary>
        /// <param name="callback">callback to invoke</param>
        /// <param name="register">set true to add callback, false to remove the callback</param>
        public World OnSet<Component>(SetComponentEventRefOnly<Component> callback, bool register = true)
        {
            if (this.TryGetWorldInfo(out var info))
            {
                var data = info.GetData<Component>();
                if (register)
                {
                    if (data.set_ref_callback == null)
                        data.set_callback += data.CallSetRefCallback;
                    data.set_ref_callback += callback;
                }
                else
                {
                    data.set_ref_callback -= callback;
                    if (data.set_ref_callback == null)
                        data.set_callback -= data.CallSetRefCallback;
                }

                data.has_set_callback = data.set_callback != null;
            }
            return this;
        }

        /// <summary>
        /// Adds a callback to be invoked whenever an entity removes a component of type
        /// </summary>
        /// <param name="callback">callback to invoke</param>
        /// <param name="register">set true to add callback, false to remove the callback</param>
        public World OnRemove<Component>(RemoveComponentEvent<Component> callback, bool register = true)
        {
            if (this.TryGetWorldInfo(out var world_info))
            {
                var data = world_info.GetData<Component>();
                if (register)
                    data.remove_callback += callback;
                else data.remove_callback -= callback;
                data.has_remove_callback = data.remove_callback != null;
            }
            return this;
        }

        /// <summary>
        /// Resizes all archetype's backing arrays to the minimum power of 2 needed to store their entities
        /// </summary>
        public void ResizeBackingArrays()
        {
            if (this.TryGetWorldInfo(out var info))
                foreach (var arch_info in info.archetypes)
                    arch_info.data?.archetype.ResizeBackingArrays();
        }

        /// <summary>
        /// Destroys all archetypes with 0 entities
        /// </summary>
        public void DestroyEmptyArchetypes()
        {
            if (this.TryGetWorldInfo(out var world_info))
            {
                foreach (var arch in world_info.archetypes)
                {
                    if (arch.data == null) continue;
                    if (arch.data.entity_count == 0)
                        arch.data.archetype.Destroy();
                }
            }
        }

        public override string ToString()
         => $"{(IsValid() ? "" : "~")}{Name} {index}.{version}";

        bool IEquatable<World>.Equals(World other) => index == other.index && version == other.version;

        public override int GetHashCode() => index;

        public override bool Equals(object obj)
        {
            if (obj is World world)
                return world == this;
            return false;
        }

        public static bool operator ==(World a, World b) => a.index == b.index && a.version == b.version;
        public static bool operator !=(World a, World b) => !(a == b);

        public static implicit operator bool(World world) => World_Info.All[world.index].version == world.version;

        /// <summary>
        /// Returns a copy of all active Worlds
        /// </summary>
        public static World[] GetAll()
        {
            var worlds = new List<World>();
            foreach (var info in World_Info.All)
            {
                if (info.data != null)
                    worlds.Add(info.data.world);
            }
            return worlds.ToArray();
        }

        /// <summary>
        /// Tries to get the entity with index.
        /// returns true if entity is valid
        /// </summary>
        public static bool TryGetEntity(int index, out Entity entity)
        {
            if (index <= Entities.Last)
            {
                var data = Entities.All[index];
                if (data.arch_info != null)
                {
                    entity = new Entity(index, data.version);
                    return true;
                }
            }
            entity = default;
            return false;
        }

        IEnumerator<Archetype> IEnumerable<Archetype>.GetEnumerator()
        {
            if (this.TryGetWorldInfo(out var info))
            {
                for (int i = 0; i < info.archetype_terminating_index; ++i)
                {
                    var arche_info = info.archetypes[i].data;
                    if (arche_info != null)
                        yield return arche_info.archetype;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (this.TryGetWorldInfo(out var info))
            {
                for (int i = 0; i < info.archetype_terminating_index; ++i)
                {
                    var arche_info = info.archetypes[i].data;
                    if (arche_info != null)
                        yield return arche_info.archetype;
                }
            }
        }
    }
}

namespace SimpleECS.Internal
{
    using System.Collections.Generic;

    public delegate void SetComponentEvent<T>(Entity entity, T old_comp, ref T new_comp);
    public delegate void SetComponentEventRefOnly<T>(Entity entity, ref T new_comp);
    public delegate void RemoveComponentEvent<T>(Entity entity, T component);

    public static partial class InternalExtensions
    {
        public static bool TryGetWorldInfo(this World world, out World_Info info)
        {
            var data = World_Info.All[world.index];
            if (data.version == world.version)
            {
                info = data.data;
                return true;
            }
            info = default;
            return false;
        }
    }

    public partial class World_Info
    {
        static World_Info()
        {
            All = new (World_Info, int)[4];
            All[0].version = 1; // added a version to the 0th index so that a default world will be invalid
        }

        public static (World_Info data, int version)[] All;
        public static int world_count;

        public string name;
        public World world;
        public World_Info(string name, World world)
        {
            this.name = name;
            this.world = world;
            archetypes[0].version++;    // this is just to prevent default archetype and default entity from being valid
            StructureEvents = new StructureEventHandler(this);
        }

        public int archetype_count => archetype_terminating_index - free_archetypes.Count;
        public int entity_count;
        public int archetype_terminating_index;
        public int archetype_structure_update_count;

        public Stack<int> free_archetypes = new Stack<int>();
        public WorldData[] world_data = new WorldData[32];

        public (Archetype_Info data, int version)[] archetypes = new (Archetype_Info, int)[32];
        public Dictionary<TypeSignature, int> signature_to_archetype_index = new Dictionary<TypeSignature, int>();

        public TypeSignature buffer_signature = new TypeSignature(); // just a scratch signature so that I'm not making new ones all the time

        public Archetype_Info GetArchetypeData(TypeSignature signature)
        {
            if (!signature_to_archetype_index.TryGetValue(signature, out var index))
            {
                if (free_archetypes.Count > 0)
                {
                    index = free_archetypes.Pop();
                    archetype_structure_update_count++;
                }
                else
                {
                    if (archetype_terminating_index == archetypes.Length)
                        System.Array.Resize(ref archetypes, archetype_terminating_index * 2);
                    index = archetype_terminating_index;
                    archetype_terminating_index++;
                }
                var sig = new TypeSignature(signature);
                signature_to_archetype_index[sig] = index;
                archetypes[index].data = new Archetype_Info(this, sig, index, archetypes[index].version);
            }
            return archetypes[index].data;
        }

        public WorldData<T> GetData<T>()
        {
            int type_id = TypeID<T>.Value;
            if (type_id >= world_data.Length)
            {
                var size = world_data.Length;
                while (size <= type_id)
                    size *= 2;
                System.Array.Resize(ref world_data, size);
            }
            if (world_data[type_id] == null)
                world_data[type_id] = new WorldData<T>();
            return (WorldData<T>)world_data[type_id];
        }

        public WorldData GetData(int type_id)
        {
            if (type_id >= world_data.Length)
            {
                var size = world_data.Length;
                while (size <= type_id)
                    size *= 2;
                System.Array.Resize(ref world_data, size);
            }
            if (world_data[type_id] == null)
            {
                var type = TypeID.Get(type_id);
                world_data[type_id] = (WorldData)System.Activator.CreateInstance(typeof(WorldData<>).MakeGenericType(type));
            }
            return world_data[type_id];
        }

        /// <summary>
        /// Handles all structural changes to the ecs world
        /// </summary>
        public StructureEventHandler StructureEvents;
    }

    public struct Entity_Info
    {
        public Archetype_Info arch_info;
        public int version;
        public int arch_index;
    }

    public static class Entities
    {
        static Entities()
        {
            All = new Entity_Info[1024];
            Free = new Queue<int>(1024);
            All[0].version++;
        }

        public static Entity_Info[] All;
        public static Queue<int> Free;
        public static int Last;
    }

    public abstract class WorldData
    {
        public bool has_remove_callback, has_set_callback;

        public abstract void Set(in Entity entity, in StructureEventHandler handler);
        public abstract void Set(in Entity entity, object component, in StructureEventHandler handler);
        public abstract void Remove(in Entity entity, in StructureEventHandler handler);
        public abstract void InvokeRemoveCallback(in Entity[] entities, in object buffer, int count);
        public abstract void InvokeRemoveCallback(in Entity entity, in object obj);

        public abstract void AddRemove(in Entity entity, object array, int index);
        public abstract void PlayBackRemove();
    }

    public sealed class WorldData<T> : WorldData
    {
        public T data;
        public SetComponentEvent<T> set_callback;
        public SetComponentEventRefOnly<T> set_ref_callback;
        public RemoveComponentEvent<T> remove_callback;

        public Queue<T> set_queue = new Queue<T>();
        public Queue<(Entity entity, T comp)> remove_queue = new Queue<(Entity entity, T comp)>();

        public override void Set(in Entity entity, in StructureEventHandler handler)
        {
            handler.Set(entity, set_queue.Dequeue());
        }

        public override void Set(in Entity entity, object component, in StructureEventHandler handler)
        {
            handler.Set(entity, (T)component);
        }

        public override void Remove(in Entity entity, in StructureEventHandler handler) => handler.Remove<T>(entity); 
        public override void InvokeRemoveCallback(in Entity[] entities, in object buffer, int count)
        {
            T[] array = (T[])buffer;
            for(int i = 0; i < count; ++ i)
                remove_callback?.Invoke(entities[i], array[i]);
        }

        public override void InvokeRemoveCallback(in Entity entity, in object obj)
            => remove_callback?.Invoke(entity, (T)obj);

        
        public void CallSetRefCallback(Entity entity, T old_comp, ref T new_comp)
        {
            set_ref_callback.Invoke(entity, ref new_comp);
        }

        public override void AddRemove(in Entity entity, object array, int index)
        {
            remove_queue.Enqueue((entity, ((T[])array)[index]));
        }

        public override void PlayBackRemove()
        {
            while(remove_queue.Count > 0)
            {
                var data = remove_queue.Dequeue();
                remove_callback?.Invoke(data.entity, data.comp);
            }
        }

        public Queue<(Entity entity, T  component)> remove_event_buffer = new Queue<(Entity entity, T component)>() ; // buffer to hold component values to avoid allocations    
    }

    public struct StructureEventHandler
    {
        public StructureEventHandler(World_Info world)
        {
            cache_events = 0;
            events = new Queue<EventData>();
            this.world = world;
        }

        World_Info world;
        int cache_events;
        public int EnqueueEvents
        {
            get => cache_events;
            set
            {
                cache_events = value;
                ExecuteEventPlayback();
            }
        }
        Queue<EventData> events;

        struct EventData
        {
            public EventType type;
            public Entity entity;
            public int type_id;
            public Archetype archetype;
            public World world;
        }

        enum EventType
        {
            CreateEntity,
            DestroyEntity,
            SetComponent,
            RemoveComponent,
            TransferEntity,
            DestroyArchetype,
            DestroyWorld,
            ResizeBackingArrays,
        }

        public void ExecuteEventPlayback()
        {
            while (cache_events == 0 && events.Count > 0)
            {
                var e = events.Dequeue();
                switch (e.type)
                {
                    case EventType.CreateEntity:
                    {
                        if (e.archetype.IsValid())
                        {
                            SetUpEntity(e.entity, world.archetypes[e.archetype.index].data);
                        }
                        else Entities.Free.Enqueue(e.entity.index);
                    }
                    break;

                    case EventType.SetComponent:
                        world.GetData(e.type_id).Set(e.entity, this);
                        //world.StructureEvents.Set(e.entity, e.type_id, );
                        break;

                    case EventType.RemoveComponent:
                        world.GetData(e.type_id).Remove(e.entity, this);
                        //Remove(e.entity, e.type_id);
                        break;

                    case EventType.DestroyEntity:
                        Destroy(e.entity);
                        break;

                    case EventType.TransferEntity:
                        Transfer(e.entity, e.world);
                        break;

                    case EventType.DestroyArchetype:
                        DestroyArchetype(e.archetype);
                        break;

                    case EventType.DestroyWorld:
                        DestroyWorld();
                        break;

                    case EventType.ResizeBackingArrays:
                        ResizeBackingArrays(e.archetype);
                        break;
                }
            }
        }

        public Entity CreateEntity(Archetype_Info archetype_data)
        {
            int index = 0;
            if (Entities.Free.Count > 0)
                index = Entities.Free.Dequeue();
            else
            {
                index = Entities.Last;
                if (index == Entities.All.Length)
                    System.Array.Resize(ref Entities.All, index * 2);
                Entities.Last++;
            }
            var version = Entities.All[index].version;
            var entity = new Entity(index, version);

            if (cache_events > 0)
            {
                Entities.All[index].version++;
                events.Enqueue(new EventData { type = EventType.CreateEntity, entity = entity, archetype = archetype_data.archetype });
            }
            else SetUpEntity(entity, archetype_data);
            return entity;
        }

        void SetUpEntity(Entity entity, Archetype_Info archetype_data)
        {
            ref var entity_data = ref Entities.All[entity.index];
            entity_data.version = entity.version;
            entity_data.arch_info = archetype_data;
            var arch_index = entity_data.arch_index = archetype_data.entity_count;
            archetype_data.entity_count++;
            archetype_data.world_info.entity_count++;
            archetype_data.EnsureCapacity(arch_index);
            archetype_data.entities[arch_index] = entity;
        }

        public void Set<Component>(in Entity entity, in Component component)
        {
            var world_data  = world.GetData<Component>();
            if (cache_events > 0)
            {
                world_data.set_queue.Enqueue(component);
                events.Enqueue(new EventData { type = EventType.SetComponent, entity = entity, type_id = TypeID<Component>.Value });
                return;
            }

            ref var entity_info = ref Entities.All[entity.index];
            if (entity_info.version == entity.version && entity_info.arch_info.TryGetArray<Component>(out var buffer))
            {
                int index = entity_info.arch_index;
                Component old = buffer[index];
                buffer[index] = component;
                world_data.set_callback?.Invoke(entity, old, ref buffer[index]);
            }
            else
            {
                var old_index = entity_info.arch_index;
                var archetype = entity_info.arch_info;
                var last_index = --archetype.entity_count;
                var last = archetype.entities[old_index] = archetype.entities[last_index];
                Entities.All[last.index].arch_index = old_index; // reassign moved entity to to index

                // adding entity to target archetype
                var target_archetype = entity_info.arch_info = world.GetArchetypeData(world.buffer_signature.Copy(archetype.signature).Add<Component>());
                var target_index = entity_info.arch_index = target_archetype.entity_count;
                target_archetype.EnsureCapacity(target_index);
                target_archetype.entity_count++;

                // moving components over
                target_archetype.entities[target_index] = entity;
                for (int i = 0; i < archetype.component_count; ++i)
                    archetype.component_buffers[i].buffer.Move(old_index, last_index, target_archetype, target_index);

                // setting the added component and calling the callback event
                if (target_archetype.TryGetArray<Component>(out var target_buffer))
                {
                    target_buffer[target_index] = component;
                    world_data.set_callback?.Invoke(entity, default, ref target_buffer[target_index]);
                }
                else
                    throw new System.Exception("Frame Work Bug");
            }
        }

        public void Remove<Component>(in Entity entity)
        {
            int type_id = TypeID<Component>.Value;
            if (cache_events > 0)
            {
                events.Enqueue(new EventData { type = EventType.RemoveComponent, entity = entity, type_id = type_id });
            }
            else
            {
                ref var entity_info = ref Entities.All[entity.index];
                if (entity.version == entity_info.version)
                {
                    var old_arch = entity_info.arch_info;
                    if (old_arch.TryGetArray<Component>(out var old_buffer))  // if archetype already has component, just set and fire event
                    {
                        var old_index = entity_info.arch_index;

                        var target_arch = world.GetArchetypeData(world.buffer_signature.Copy(old_arch.signature).Remove(type_id));
                        var target_index = target_arch.entity_count;
                        target_arch.entity_count++;
                        target_arch.EnsureCapacity(target_index);

                        old_arch.entity_count--;
                        var last_index = old_arch.entity_count;
                        var last = old_arch.entities[old_index] = old_arch.entities[last_index];
                        Entities.All[last.index].arch_index = old_index;

                        entity_info.arch_index = target_index;
                        entity_info.arch_info = target_arch;

                        target_arch.entities[target_index] = entity;
                        var removed = old_buffer[old_index];
                        for (int i = 0; i < old_arch.component_count; ++i)
                            old_arch.component_buffers[i].buffer.Move(old_index, last_index, target_arch, target_index);
                        world.GetData<Component>().remove_callback?.Invoke(entity, removed);
                    }
                }
            }
        }

        public void Transfer(Entity entity, World target_world)
        {
            if (cache_events > 0)
                events.Enqueue(new EventData { type = EventType.TransferEntity, entity = entity, world = target_world });
            else
            {
                ref var entity_info = ref Entities.All[entity.index];
                if (entity_info.version == entity.version
                    && entity_info.arch_info.world_info.world != target_world
                    && target_world.TryGetWorldInfo(out var target_world_info))
                {
                    var target_arch = target_world_info.GetArchetypeData(entity_info.arch_info.signature);
                    var target_index = target_arch.entity_count;
                    target_arch.EnsureCapacity(target_index);
                    target_arch.entity_count++;
                    target_arch.world_info.entity_count++;

                    var old_index = entity_info.arch_index;
                    var old_arch = entity_info.arch_info;
                    var last_index = --old_arch.entity_count;
                    --old_arch.world_info.entity_count;

                    var last = old_arch.entities[old_index] = old_arch.entities[last_index];
                    Entities.All[last.index].arch_index = old_index;
                    target_arch.entities[target_index] = entity;

                    for (int i = 0; i < old_arch.component_count; ++i)
                        old_arch.component_buffers[i].buffer.Move(old_index, last_index, target_arch.component_buffers[i].buffer.array, target_index);

                    entity_info.arch_index = target_index;
                    entity_info.arch_info = target_arch;
                }
            }
        }

        public void Destroy(Entity entity)
        {
            if (cache_events > 0)
                events.Enqueue(new EventData { type = EventType.DestroyEntity, entity = entity });
            else
            {
                ref var entity_info = ref Entities.All[entity.index];
                if (entity_info.version == entity.version)
                {
                    var old_arch = entity_info.arch_info;
                    var old_index = entity_info.arch_index;
                    --old_arch.entity_count;
                    --world.entity_count;
                    var last_index = old_arch.entity_count;
                    var last = old_arch.entities[old_index] = old_arch.entities[last_index];    // swap 
                    Entities.All[last.index].arch_index = old_index;

                    var to_call = new (WorldData callback, object removed)[old_arch.component_count];   // this causes boxing, unsure how to cleanly do this
                    int to_call_count = 0;

                    for (int i = 0; i < old_arch.component_count; ++i)
                    {
                        var pool = old_arch.component_buffers[i];
                        var callback = world.GetData(pool.type_id);
                        if (callback.has_remove_callback)
                        {
                            to_call[to_call_count] = (callback, pool.buffer.Get(entity_info.arch_index));
                            to_call_count ++;                    
                        }
                        pool.buffer.Remove(old_index, last_index);
                    }

                    entity_info.version++;
                    entity_info.arch_info = default;
                    Entities.Free.Enqueue(entity.index);

                    for(int i = 0; i < to_call_count; ++ i)
                        to_call[i].callback.InvokeRemoveCallback(entity, to_call[i].removed);
                }
            }
        }

        public void DestroyArchetype(Archetype archetype)
        {
            if (cache_events > 0)
            {
                events.Enqueue(new EventData { type = EventType.DestroyArchetype, archetype = archetype });
            }
            else
            {
                if (archetype.TryGetArchetypeInfo(out var arch_info))
                {
                    world.entity_count -= arch_info.entity_count;
                    world.signature_to_archetype_index.Remove(arch_info.signature);   // update archetype references
                    world.archetypes[archetype.index].version++;
                    world.archetypes[archetype.index].data = default;
                    world.free_archetypes.Push(archetype.index);
                    world.archetype_structure_update_count++;

                    for (int i = 0; i < arch_info.entity_count; ++i)    // remove entities from world
                    {
                        var entity = arch_info.entities[i];
                        ref var info = ref Entities.All[entity.index];
                        info.version++;
                        info.arch_info = default;
                        Entities.Free.Enqueue(entity.index);
                    }

                    for (int i = 0; i < arch_info.component_count; ++i) // invoke callbacks
                    {
                        var pool = arch_info.component_buffers[i];
                        var callback = world.GetData(pool.type_id);
                        if (callback.has_remove_callback)
                        {
                            callback.InvokeRemoveCallback(arch_info.entities, pool.buffer.array, arch_info.entity_count);
                        }
                    }
                }
            }
        }

        public void DestroyWorld()
        {
            if (cache_events > 0)
                events.Enqueue(new EventData { type = EventType.DestroyWorld });
            else
            {
                ref var world_info = ref World_Info.All[world.world.index];
                if (world_info.version == world.world.version)  // still needs to be checked incase multiple destorys are queued
                {
                    world_info.version++;
                    var data = world_info.data;
                    world_info.data = default;

                    foreach (var archetype in data.archetypes)   // delete all entities first
                    {
                        var arche_info = archetype.data;
                        if (arche_info == null) continue;

                        for (int i = 0; i < arche_info.entity_count; ++i)
                        {
                            var index = arche_info.entities[i].index;
                            ref var info = ref Entities.All[index];
                            info.version++;
                            info.arch_info = default;
                            Entities.Free.Enqueue(index);
                        }
                    }
                    
                    foreach (var archetype in data.archetypes) // then do all their callbacks
                    {
                        var arche_info = archetype.data;
                        if (arche_info == null) continue;
                        for (int i = 0; i < arche_info.component_count; ++i)
                        {
                            var pool = arche_info.component_buffers[i];
                            var world_data = data.GetData(pool.type_id);
                            if (world_data.has_remove_callback)
                            {
                                world_data.InvokeRemoveCallback(arche_info.entities, pool.buffer.array, arche_info.entity_count);
                            }
                        }
                    }
                }
            }
        }

        public void ResizeBackingArrays(Archetype archetype)
        {
            if (cache_events > 0)
                events.Enqueue(new EventData { type = EventType.ResizeBackingArrays, archetype = archetype });
            else
                if (archetype.TryGetArchetypeInfo(out var info))
                info.ResizeBackingArrays();
        }
    }

}