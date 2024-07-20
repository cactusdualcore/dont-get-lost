﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICharacterController
{
    void control(character c);
    void draw_gizmos();
    void draw_inspector_gui();
    void on_end_control(character c);
    string inspect_info();
}

public interface IAcceptsDamage
{
    void take_damage(int damage);
}

public class character : networked,
    INotPathBlocking, IDontBlockItemLogisitcs,
    IAcceptsDamage, IPlayerInteractable, IPathingAgent,
    INotCoverFromElements, town_path_element.path.ITownWalker,
    IBlocksInteractionPropagation
{
    //############################################//
    // Parameters determining character behaviour //
    //############################################//

    public const float AGRO_RANGE = 8f;
    public const float IDLE_WALK_RANGE = 5f;

    public string display_name;
    public string plural_name;

    public float pathfinding_resolution = 0.5f;
    public float height = 2f;
    public float walk_speed = 1f;
    public float run_speed = 4f;
    public float rotation_lerp_speed = 1f;
    public bool can_walk = true;
    public bool can_swim = false;
    public bool align_to_terrain = false;
    public float align_to_terrain_amount = 1.0f;

    public int max_health = 10;
    public FRIENDLINESS friendliness;
    public float attack_range = 1f;
    public float attack_time = 1f;
    public int attack_damage = 50;

    /// <summary> The object currently controlling this character. </summary>
    public ICharacterController controller
    {
        get
        {
            // Default control
            if (_controller == null)
                _controller = default_controller();

            return _controller;
        }
        set
        {
            if (_controller == value)
                return; // No change

            var tmp = _controller;
            _controller = value;

            if (tmp != null)
                tmp.on_end_control(this);
        }
    }
    ICharacterController _controller;

    public enum FRIENDLINESS
    {
        AGRESSIVE,
        FRIENDLY,
        AFRAID
    }

    public float combat_level => 0.01f * attack_damage * max_health / attack_time;

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public virtual player_interaction[] player_interactions(RaycastHit hit)
    {
        return new player_interaction[]
        {
            new player_inspectable(transform)
            {
                text= () =>
                {
                    return display_name.capitalize() + "\n" +
                           "Combat level : "+combat_level + "\n"+
                           controller?.inspect_info();
                }
            }
        };
    }

    //####################################//
    // town_path_element.path.ITownWalker //
    //####################################//

    public virtual town_path_element find_best_path_element() => town_path_element.nearest_element(transform.position);

    public town_path_element town_path_element
    {
        get
        {
            if (_town_path_element == null)
                _town_path_element = find_best_path_element();
            return _town_path_element;
        }
        set
        {
            if (this == null)
            {
                // This == null => don't call on_character callbacks
                _town_path_element = value;
                return;
            }

            if (_town_path_element == value)
                _town_path_element?.on_character_move_towards(this);
            else
            {
                _town_path_element?.on_character_leave(this);
                _town_path_element = value;
                _town_path_element?.on_character_enter(this);
            }
        }
    }
    town_path_element _town_path_element;

    public int group => town_path_element == null ? -1 : town_path_element.group;
    public int room => town_path_element == null ? -1 : town_path_element.room;

    public void on_walk_towards(town_path_element element) => town_path_element = element;
    public void on_end_walk() => town_path_element?.on_character_leave(this);

    public void look_at(Vector3 v, bool stay_upright = true)
    {
        Vector3 delta = v - transform.position;
        if (stay_upright) delta.y = 0;
        if (delta.magnitude < 0.001f) return;
        transform.forward = delta;
    }

    //########//
    // SOUNDS //
    //########//

    Dictionary<character_sound.TYPE, List<character_sound>> sounds =
        new Dictionary<character_sound.TYPE, List<character_sound>>();

    AudioSource sound_source;

    void load_sounds()
    {
        // Record all of the sounds I can make
        Vector3 sound_centre = transform.position;
        foreach (var s in GetComponentsInChildren<character_sound>())
        {
            List<character_sound> type_sounds;
            if (!sounds.TryGetValue(s.type, out type_sounds))
            {
                type_sounds = new List<character_sound>();
                sounds[s.type] = type_sounds;
            }
            type_sounds.Add(s);

            sound_centre = s.transform.position;
        }

        // Normalize probabilities (if they sum to more than 1)
        foreach (var kv in sounds)
        {
            var list = kv.Value;
            float total_prob = 0;
            foreach (var s in list) total_prob += s.probability;

            if (total_prob > 1f)
                foreach (var s in list)
                    s.probability /= total_prob;
        }

        if (sound_source == null)
        {
            sound_source = new GameObject("sound_source").AddComponent<AudioSource>();
            sound_source.transform.position = sound_centre;
            sound_source.transform.SetParent(transform);
            sound_source.spatialBlend = 1f; // 3D
        }
    }

    public void play_random_sound(character_sound.TYPE type)
    {
        // Don't play sounds if dead
        if (is_dead) return;

        List<character_sound> sound_list;
        if (!sounds.TryGetValue(type, out sound_list))
        {
            Debug.Log("No character sounds of type " + type + " for " + name);
            return;
        }

        character_sound chosen = null;
        float rnd = Random.Range(0, 1f);
        float total = 0;
        foreach (var s in sound_list)
        {
            total += s.probability;
            if (total > rnd)
            {
                chosen = s;
                break;
            }
        }

        if (chosen == null)
            return;

        sound_source.Stop();
        sound_source.pitch = chosen.pitch_modifier * Random.Range(0.95f, 1.05f);
        sound_source.clip = chosen.clip;
        sound_source.volume = chosen.volume;
        sound_source.Play();
    }

    public void play_idle_sounds()
    {
        // Play idle sounds
        if (!sound_source.isPlaying)
            if (Random.Range(0, 1f) < 0.1f)
                play_random_sound(character_sound.TYPE.IDLE);
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    protected virtual void Start()
    {
        load_sounds();
        InvokeRepeating("slow_update", Random.Range(0, 1f), 1f);
        characters.Add(this);
    }

    protected virtual void OnDestroy()
    {
        attacker_entrypoint.validate_attackers(being_destroyed: new HashSet<character> { this });
        town_path_element = null;
        characters.Remove(this);
    }

    void slow_update()
    {
        if (!(this is settler))
            play_idle_sounds();
    }

    protected virtual void Update()
    {
        // Characters are controlled by the authority client
        if (this == null || !has_authority) return;

        // Don't do anyting unless the chunk is generated
        if (!chunk.generation_complete(transform.position)) return;

        // Don't do anything if dead
        if (is_dead) return;

        controller?.control(this);
    }

    private void OnDrawGizmos()
    {
        if (is_dead) return;

        controller?.draw_gizmos();
        Vector3 f = transform.forward * pathfinding_resolution * 0.5f;
        Vector3 r = transform.right * pathfinding_resolution * 0.5f;
        Vector3[] square = new Vector3[]
        {
            transform.position + f + r,
            transform.position + f - r,
            transform.position - f - r,
            transform.position - f + r,
            transform.position + f + r
        };

        Gizmos.color = Color.green;
        for (int i = 1; i < square.Length; ++i)
            Gizmos.DrawLine(square[i - 1], square[i]);

        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * height);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, 2 * new Vector3(attack_range, 0, attack_range));
    }

    private void OnDrawGizmosSelected()
    {
        if (town_path_element != null)
        {
            Gizmos.color = new Color(0, 1, 1, 0.5f);
            Gizmos.matrix = town_path_element.transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, new Vector3(1, 0.1f, 1));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }

    //############//
    // NETWORKING //
    //############//

    networked_variables.net_float y_rotation;
    networked_variables.net_int health;
    networked_variables.net_int awareness;
    networked_variables.net_bool dont_despawn_automatically;
    inventory loot;

    public bool despawns_automatically
    {
        get => !dont_despawn_automatically.value;
        set => dont_despawn_automatically.value = !value;
    }

    public override void on_init_network_variables()
    {
        y_rotation = new networked_variables.net_float(resolution: 5f);
        y_rotation.on_change = () =>
        {
            var ea = transform.rotation.eulerAngles;
            ea.y = y_rotation.value;
            transform.rotation = Quaternion.Euler(ea);
        };

        health = new networked_variables.net_int(
            default_value: max_health,
            min_value: 0, max_value: max_health);

        health.on_change = () =>
        {
            healthbar.set_fraction(health.value / (float)max_health);
            healthbar.gameObject.SetActive(health.value != 0 && health.value != max_health);
            if (health.value <= 0)
                die();
        };

        awareness = new networked_variables.net_int(
            default_value: 0, min_value: 0, max_value: 100);

        awareness.on_change = () =>
        {
            awareness_meter.set_fraction(awareness.value / 100f);
            awareness_meter.gameObject.SetActive(awareness.value != 0 && awareness.value != 100);
        };

        dont_despawn_automatically = new networked_variables.net_bool();
    }

    public override void on_network_update()
    {
        if (has_authority)
        {
            networked_position = transform.position;
            y_rotation.value = transform.rotation.eulerAngles.y;
        }
    }

    public override void on_add_networked_child(networked child)
    {
        if (child is inventory)
        {
            var inv = (inventory)child;
            if (inv.name.Contains("loot"))
            {
                loot = inv;
                loot.ui.GetComponentInChildren<UnityEngine.UI.Text>().text = "Dead " + display_name;
            }
        }
    }

    public override bool persistant()
    {
        // Characters despawn when not loaded
        return false;
    }

    //########//
    // HEALTH //
    //########//

    public int remaining_health { get => health.value; }

    public void heal(int amount)
    {
        int max_heal = max_health - health.value;
        health.value += Mathf.Min(amount, max_heal);
    }

    public void take_damage(int damage)
    {
        var hm = hit_marker.create("-" + damage);
        hm.transform.position = transform.position + transform.up * height;
        awareness.value = 100;
        play_random_sound(character_sound.TYPE.INJURY);
        health.value -= damage;
    }

    healthbar healthbar
    {
        get
        {
            if (_healthbar == null)
            {
                _healthbar = new GameObject("healthbar").AddComponent<healthbar>();
                _healthbar.transform.SetParent(transform);
                _healthbar.transform.position = transform.position + transform.up * height;
            }
            return _healthbar;
        }
    }
    healthbar _healthbar;

    //###########//
    // AWARENESS //
    //###########//

    public void modify_awareness(int delta) { awareness.value += delta; }
    public bool is_aware
    {
        get => awareness.value > 99;
        set => awareness.value = value ? 100 : 0;
    }

    healthbar awareness_meter
    {
        get
        {
            if (_awareness_meter == null)
            {
                _awareness_meter = new GameObject("awareness_meter").AddComponent<healthbar>();
                _awareness_meter.transform.SetParent(transform);
                _awareness_meter.transform.position = transform.position + Vector3.up * (height + 0.1f);
                _awareness_meter.height = 5;
                _awareness_meter.foreground_color = Color.yellow;
                _awareness_meter.background_color = Color.black;
            }
            return _awareness_meter;
        }
    }
    healthbar _awareness_meter;

    float delta_awareness
    {
        get => _delta_awareness;
        set
        {
            _delta_awareness = value;

            if (_delta_awareness > 1f)
            {
                int da = Mathf.FloorToInt(_delta_awareness);
                _delta_awareness -= da;
                modify_awareness(da);
            }
            else if (_delta_awareness < -1f)
            {
                int da = Mathf.FloorToInt(-_delta_awareness);
                _delta_awareness += da;
                modify_awareness(-da);
            }
        }
    }
    float _delta_awareness;

    public void run_awareness_checks(player p)
    {
        const float CUTOFF_RADIUS = 16f;    // Ignore players beyond this
        const float MIN_AWARE_TIME = 0.25f; // The minimum amount of time it takes to become aware
        const float DEAWARE_TIME = 4f;      // The amount of time it takes to become fully un-aware              

        // If we're aware, stay that way
        if (is_aware) return;

        Vector3 delta = p.transform.position - transform.position;

        if (delta.magnitude > CUTOFF_RADIUS)
        {
            // Not in sight (too far away)
            delta_awareness -= 100f * Time.deltaTime / DEAWARE_TIME;
            return;
        }

        // A measure of proximity 1 => very close, 0 => very far
        float prox = 1f - delta.magnitude / CUTOFF_RADIUS;
        prox = prox * prox;

        // Field of view for awareness (increases as we get closer)
        float fov = 90f + (360 - 90) * prox;

        if (Vector3.Angle(delta, transform.forward) > fov / 2f)
        {
            // Not in sight (out of fov)
            delta_awareness -= 100f * Time.deltaTime / DEAWARE_TIME;
            return;
        }

        var ray = new Ray(transform.position + height * Vector3.up / 2f, delta);
        foreach (var hit in Physics.RaycastAll(ray, delta.magnitude))
        {
            if (hit.transform.IsChildOf(transform)) continue; // Can't block my own vision
            if (hit.transform.IsChildOf(p.transform)) break; // Found the player

            // Vision is blocked
            delta_awareness -= 100f * Time.deltaTime / DEAWARE_TIME;
            return;
        }

        // I can see the player, increase awareness at a rate depending on proximity
        delta_awareness += 100f * Time.deltaTime * prox / MIN_AWARE_TIME;
    }

    //#######//
    // DEATH //
    //#######//

    dead_character dead_version;
    public bool is_dead => (dead_version != null) || (health != null && health.value <= 0);

    void die()
    {
        if (dead_version == null && create_dead_body())
            dead_version = dead_character.create(this);

        attacker_entrypoint.validate_attackers();

        on_death();
    }

    protected virtual bool create_dead_body() { return true; }
    protected virtual void on_death() { }

    public class dead_character : MonoBehaviour, INotPathBlocking, IPlayerInteractable
    {
        character character;

        player_interaction[] _interactions;
        public player_interaction[] player_interactions(RaycastHit hit)
        {
            if (_interactions == null) _interactions = new player_interaction[]
            {
                new player_inspectable(transform)
                {
                    text = ()=> "Dead "+character.display_name
                },
                new loot_menu(this)
            };
            return _interactions;
        }

        class loot_menu : left_player_menu
        {
            dead_character dc;
            public loot_menu(dead_character dc) : base("dead " + dc.character.display_name) => this.dc = dc;
            protected override RectTransform create_menu(Transform parent) => dc.character.loot?.ui;
            public override inventory editable_inventory() => dc.character.loot;
        }

        void on_create(character to_copy)
        {
            foreach (var r in to_copy.GetComponentsInChildren<MeshRenderer>())
            {
                // Create a render-only copy of each render in the character
                var rcopy = r.inst();

                // Don't copy children (they will be copied later in the
                // GetComponentsInChildren loop)
                foreach (Transform child in rcopy.transform)
                    Destroy(child.gameObject);

                // Move the copy to the exact same place as the original mesh
                rcopy.transform.position = r.transform.position;
                rcopy.transform.rotation = r.transform.rotation;
                rcopy.transform.localScale = r.transform.lossyScale;

                // Delete anything that isn't to do with rendering (perhaps
                // this method could be improved by simply building an object
                // that only has the desired stuff, rather than deleting stuff)
                foreach (var c in rcopy.GetComponentsInChildren<Component>())
                {
                    if (c is Transform) continue;
                    if (c is MeshRenderer) continue;
                    if (c is MeshFilter) continue;
                    if (c is MeshCollider)
                    {
                        // Make sure all colliders are convex so we
                        // can add rigidbodies later
                        var mc = (MeshCollider)c;
                        mc.convex = true;
                        continue;
                    }
                    Destroy(c);
                }

                // Make the copy a child of this dead_character and
                // give it a simple collider
                rcopy.transform.SetParent(transform);

                // Make the character invisisble (do this after
                // we copy, so the copied version isn't invisible)
                r.enabled = false;
            }

            // Delay rigidbodies so they have time to register the new colliders
            Invoke("add_rigidbodies", 0.1f);
        }

        void add_rigidbodies()
        {
            foreach (Transform c in transform)
            {
                var rb = c.gameObject.AddComponent<Rigidbody>();

                // Don't let the body parts ping around everywhere
                // (as fun as that is)
                rb.maxAngularVelocity = 1f;
                rb.maxDepenetrationVelocity = 0.25f;
            }
        }

        public static dead_character create(character to_copy)
        {
            // Create the dead version
            var dead_version = new GameObject("dead_" + to_copy.name).AddComponent<dead_character>();
            dead_version.character = to_copy;
            dead_version.transform.position = to_copy.transform.position;
            dead_version.transform.rotation = to_copy.transform.rotation;
            dead_version.on_create(to_copy);

            // Deactivate the alive version
            foreach (var r in to_copy.GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in to_copy.GetComponentsInChildren<Collider>()) c.enabled = false;
            to_copy.healthbar.gameObject.SetActive(false);
            to_copy.awareness_meter.gameObject.SetActive(false);

            // Create loot on the authority client
            if (to_copy.has_authority)
            {
                // Create the looting inventory
                var loot = (inventory)client.create(
                    to_copy.transform.position,
                    "inventories/character_loot",
                    parent: to_copy);

                // Add loot to the above inventory once it's registered
                loot.add_register_listener(() =>
                {
                    foreach (var p in to_copy.GetComponents<item_product>())
                        p.create_in(loot);
                });
            }

            // Parent the dead version to the character so they get despawned together
            dead_version.transform.SetParent(to_copy.transform);
            dead_version.Invoke("decay", 60);
            return dead_version;
        }

        void decay()
        {
            character.delete();
        }
    }

    //#########//
    // CONTROL //
    //#########//

    protected virtual ICharacterController default_controller()
    {
        return new default_character_control();
    }

    bool unstuck_pos_valid(Vector3 unstuck_pos)
    {
        // Check if unstuck position is too close to current position
        const float MIN_MOVE = 0.1f;
        if ((unstuck_pos - transform.position).sqrMagnitude < MIN_MOVE * MIN_MOVE) return false;
        return true;
    }

    /// <summary> Call to put the character somewhere sensible. </summary>
    public void unstuck()
    {
        // First, attempt to unstick onto terrain
        var tc = utils.raycast_for_closest<TerrainCollider>(new Ray(
            transform.position + Vector3.up * world.MAX_ALTITUDE, Vector3.down),
            out RaycastHit hit);

        if (tc != null && unstuck_pos_valid(hit.point))
        {
            // Valid point on terrain found, go there
            transform.position = hit.point;
            return;
        }

        // Then attempt to unstick onto anything (other than myself)
        var col = utils.raycast_for_closest<Collider>(new Ray(
            transform.position + Vector3.up * world.MAX_ALTITUDE, Vector3.down),
            out hit, accept: (h, c) => !c.transform.IsChildOf(transform));

        if (col != null && unstuck_pos_valid(hit.point))
        {
            // Valid point on collider found, go there
            transform.position = hit.point;
            return;
        }

        // Just diffuse around (Nudge character)
        transform.position += Random.onUnitSphere * 0.5f;
    }

    public bool move_towards(Vector3 point, float speed, out bool failed, float arrive_distance = -1)
    {
        if (arrive_distance < 0)
            arrive_distance = pathfinding_resolution / 2f;

        // Work out how far to the point
        Vector3 delta = point - transform.position;
        float dis = Time.deltaTime * speed;

        Vector3 new_pos = transform.position;

        if (delta.magnitude < dis) new_pos += delta;
        else new_pos += delta.normalized * dis;

        failed = false;
        if (!is_allowed_at(new_pos))
        {
            failed = true;
            return false;
        }

        // Move along to the new position
        transform.position = new_pos;

        // Look in the direction of travel
        delta.y = 0;
        if (delta.magnitude > 10e-4)
        {
            // Lerp forward look direction
            Vector3 new_forward = Vector3.Lerp(
                transform.forward,
                delta.normalized,
                rotation_lerp_speed * speed * Time.deltaTime
            );

            if (new_forward.magnitude > 10e-4)
            {
                // Set up direction with reference to legs
                Vector3 up = Vector3.zero;
                if (align_to_terrain)
                    foreach (var l in GetComponentsInChildren<leg>())
                        up += l.ground_normal * align_to_terrain_amount +
                              Vector3.up * (1 - align_to_terrain_amount);
                else up = Vector3.up;

                up = Vector3.Lerp(
                    transform.up,
                    up.normalized,
                    rotation_lerp_speed * speed * Time.deltaTime
                );

                new_forward -= Vector3.Project(new_forward, up);
                transform.rotation = Quaternion.LookRotation(
                    new_forward,
                    up.normalized
                );
            }
        }

        return (point - new_pos).magnitude < arrive_distance;
    }

    public Vector3 projectile_target() => transform.position + Vector3.up * height / 2f;

    //###############//
    // IPathingAgent //
    //###############//

    public bool is_allowed_at(Vector3 v)
    {
        // Check we're in the right medium
        if (!can_swim && v.y < world.SEA_LEVEL) return false;
        if (!can_walk && v.y > world.SEA_LEVEL) return false;
        return true;
    }

    public Vector3 validate_position(Vector3 v, out bool valid)
    {
        if (v.y < world.SEA_LEVEL)
        {
            v.y = world.SEA_LEVEL;
            valid = can_swim;
            return v;
        }

        Vector3 ret = pathfinding_utils.validate_walking_position(
            v, resolution, out valid, settings: GetComponent<pathfinding_overrides>());
        if (!is_allowed_at(ret)) valid = false;
        return ret;
    }

    public bool validate_move(Vector3 a, Vector3 b)
    {
        return pathfinding_utils.validate_walking_move(a, b,
            resolution, height, GetComponent<pathfinding_overrides>());
    }

    public float resolution => pathfinding_resolution;

    //##########//
    // PRODUCTS //
    //##########//

    public class looting_products : product_list_recipe_info
    {
        public looting_products(IEnumerable<product> products) : base(products) { }
        public override string recipe_book_string() => "Loot -> " + product.product_quantities_list(new List<product>(products));
        public override float average_ingredients_value() => 0f;
    }

    public IRecipeInfo looting_products_recipe()
    {
        var products = GetComponentsInChildren<product>();
        return products.Length == 0 ? null : new looting_products(products);
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<character> characters;

    static HashSet<character> natural_characters
    {
        get
        {
            HashSet<character> ret = new HashSet<character>();
            foreach (var c in characters)
            {
                // Settlers/visitors aren't natual
                if (c is settler) continue;
                if (c is visiting_character) continue;

                // Things set to not despawn aren't natural
                if (c.dont_despawn_automatically.value) continue;

                ret.Add(c);
            }
            return ret;
        }
    }

    public static bool characters_enabled
    {
        get => _characters_enabled;
        set
        {
            if (_characters_enabled == value)
                return; // No change

            _characters_enabled = value;
            if (!_characters_enabled)
                foreach (var c in FindObjectsOfType<character>())
                    c.delete();
        }
    }
    static bool _characters_enabled = true;

    public static int target_character_count => character_spawn_point.active_count;

    public static void initialize()
    {
        characters = new HashSet<character>();
    }

    public static void run_spawning()
    {
        if (!characters_enabled) return;

        var nat_characters = natural_characters;

        if (nat_characters.Count < target_character_count)
        {
            // Fewer characters than target, spawn one
            character_spawn_point.spawn();
        }
        else if (nat_characters.Count > target_character_count)
        {
            // More characters than target, despawn the character 
            // that is furthest from the player
            var furthest = utils.find_to_min(nat_characters, (c) =>
            {
                if (c == null) return Mathf.Infinity;

                // Only delete default-controlled characters
                if (c.controller is default_character_control)
                    return -(c.transform.position - player.current.transform.position).sqrMagnitude;

                return Mathf.Infinity;
            });
            furthest?.delete();
        }
    }

    public static string info() =>
        "    Total characters   : " + characters.Count + "\n" +
        "    Natural characters : " + natural_characters.Count + "/" + target_character_count;

    //########//
    // EDITOR //
    //########//

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(character), true)]
    [UnityEditor.CanEditMultipleObjects]
    new public class editor : networked.editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var c = (character)target;
            c.controller?.draw_inspector_gui();
        }
    }
#endif
}

//###########//
// HEALTHBAR //
//###########//

class healthbar : MonoBehaviour
{
    public int width = 100;
    public int height = 10;
    public Color background_color = Color.red;
    public Color foreground_color = Color.green;

    RectTransform canv_rect;
    UnityEngine.UI.Image foreground;

    public void set_fraction(float f)
    {
        if (foreground == null) create();

        f = Mathf.Clamp(f, 0, 1f);
        foreground.GetComponent<RectTransform>().sizeDelta = new Vector2(
            canv_rect.sizeDelta.x * f,
            canv_rect.sizeDelta.y
        );
    }

    private void create()
    {
        var canv = gameObject.AddComponent<Canvas>();
        canv.renderMode = RenderMode.WorldSpace;
        canv.worldCamera = player.current.camera;
        canv_rect = canv.GetComponent<RectTransform>();
        canv_rect.SetParent(transform);
        canv_rect.localRotation = Quaternion.identity;
        canv_rect.sizeDelta = new Vector2(width, height);

        var background = new GameObject("background").AddComponent<UnityEngine.UI.Image>();
        foreground = new GameObject("foreground").AddComponent<UnityEngine.UI.Image>();
        background.color = background_color;
        foreground.color = foreground_color;
        var background_rect = background.GetComponent<RectTransform>();
        var foreground_rect = foreground.GetComponent<RectTransform>();

        background_rect.SetParent(canv_rect);
        foreground_rect.SetParent(canv_rect);

        background_rect.sizeDelta = canv_rect.sizeDelta;
        foreground_rect.sizeDelta = canv_rect.sizeDelta;

        background_rect.localPosition = Vector3.zero;
        foreground_rect.localPosition = Vector3.zero;

        background_rect.anchoredPosition = Vector2.zero;
        foreground_rect.anchoredPosition = Vector2.zero;

        background_rect.localRotation = Quaternion.identity;
        foreground_rect.localRotation = Quaternion.identity;

        canv_rect.transform.localScale = new Vector3(1f, 1f, 1f) / width;
    }
}

//###########################//
// DEFAULT CHARACTER CONTROL //
//###########################//

public class idle_wander : ICharacterController
{
    random_path path;
    int index;
    bool going_forward;

    public void control(character c)
    {
        if (path == null)
        {
            Vector3 start = c.transform.position;
            random_path.success_func sf = (v) => (v - start).magnitude > character.IDLE_WALK_RANGE;
            path = new random_path(start, c, sf, midpoint_successful: sf);
            path.on_invalid_start = () => c?.unstuck();
            path.on_invalid_end = () => c?.unstuck();
            index = 0;
            going_forward = true;
        }

        switch (path.state)
        {
            case global::path.STATE.SEARCHING:
                path.pathfind(load_balancing.iter);
                break;

            case global::path.STATE.FAILED:
                path = null;
                break;

            case global::path.STATE.COMPLETE:
                walk_path(c);
                break;

            default:
                Debug.LogError("Unkown path state in idle_wander!");
                break;
        }
    }

    void walk_path(character c)
    {
        if (path.length < 2)
        {
            path = null;
            return;
        }

        // Walked off the start of the path - change direction
        if (index < 0)
        {
            going_forward = true;
            index = 1;
        }

        // Walked off the end of the path - change direction
        if (index >= path.length)
        {
            going_forward = false;
            index = path.length - 1;
        }

        if (c.move_towards(path[index], c.walk_speed, out bool failed))
        {
            if (going_forward) ++index;
            else --index;
        }

        if (failed) path = null;
    }

    public void on_end_control(character c) { }
    public void draw_gizmos() => path?.draw_gizmos();
    public void draw_inspector_gui() { }
    public string inspect_info() => "Wandering idly";
}

public class flee_controller : ICharacterController
{
    flee_path path;
    int index;
    Transform fleeing;

    public flee_controller(Transform fleeing)
    {
        // The object we are fleeing from
        this.fleeing = fleeing;
    }

    public void control(character c)
    {
        if (fleeing == null)
        {
            Debug.LogError("You should probably think about this case more; somehow we need to return to idle...");
            return;
        }

        if (path == null)
        {
            // Get a new fleeing path
            path = new flee_path(c.transform.position, fleeing, c);
            path.on_invalid_start = () => c?.unstuck();
            index = 0;
        }

        switch (path.state)
        {
            case global::path.STATE.SEARCHING:
                path.pathfind(load_balancing.iter * 2);
                break;

            case global::path.STATE.FAILED:
                path = null;
                break;

            case global::path.STATE.COMPLETE:
                walk_path(c);
                break;

            default:
                Debug.LogError("Unkown path state in flee_controller!");
                break;
        }
    }

    void walk_path(character c)
    {
        if (path.length <= index)
        {
            // Reached the end of the path, time for a new one
            path = null;
            return;
        }

        // Walk along the path
        if (c.move_towards(path[index], c.run_speed, out bool failed))
            ++index;

        if (failed) path = null;
    }

    public void on_end_control(character c) { }
    public void draw_gizmos() { path?.draw_gizmos(); }
    public void draw_inspector_gui() { }
    public string inspect_info() { return "Fleeing"; }
}

public class attack_controller : ICharacterController
{
    Transform target;
    float attack_progress = 0;
    bool striked_this_cycle = false;
    IAcceptsDamage to_damage;

    Vector3 attack_start_location;
    Vector3 attack_direction;
    Vector3 attack_ground_normal;

    List<arm> arms;
    List<Transform> arm_targets;

    public attack_controller(character c, Transform target, IAcceptsDamage to_damage)
    {
        this.target = target;
        this.to_damage = to_damage;
        arms = new List<arm>(c.GetComponentsInChildren<arm>());
        arm_targets = new List<Transform>();
        foreach (var a in arms)
        {
            var at = new GameObject("arm_target").transform;
            arm_targets.Add(at);
            a.to_grab = at;
            at.transform.position = a.shoulder.position + c.transform.forward * a.total_length / 2f;
        }

        // Figure out the ground normal here
        attack_ground_normal = Vector3.up;
        foreach (var hit in Physics.RaycastAll(c.transform.position + Vector3.up, Vector3.down, 2f))
        {
            if (hit.transform.IsChildOf(target) || hit.transform.IsChildOf(c.transform))
                continue;

            attack_ground_normal = hit.normal;
            break;
        }

        attack_start_location = c.transform.position;
        attack_direction = target.transform.position - attack_start_location;
        attack_direction -= Vector3.Project(attack_direction, attack_ground_normal);
        attack_direction.Normalize();
    }

    public void control(character c)
    {
        attack_progress += Time.deltaTime / c.attack_time;

        // Orient along attack direction
        c.transform.forward = Vector3.Lerp(c.transform.forward, attack_direction, 0.2f);

        // Strike curve approaches 1 at point of strike, 0 at start position
        float strike_curve =
            Mathf.Sin(Mathf.Pow(attack_progress, 2) * Mathf.PI) -
            Mathf.Sin(attack_progress * Mathf.PI) / 3f;

        const float curve_max_at = 0.74121f;

        if (attack_progress > 1f)
        {
            // Move to next cycle
            striked_this_cycle = false;
            attack_progress = 0f;
        }

        if (!striked_this_cycle && attack_progress > curve_max_at)
        {
            // Strike
            striked_this_cycle = true;
            if ((c.transform.position - target.transform.position).magnitude < c.attack_range)
                to_damage.take_damage(c.attack_damage);
        }

        // Strike animation (i.e. lunge)
        Vector3 target_pos = attack_start_location + attack_direction * strike_curve;
        utils.move_towards(c.transform, target_pos, c.run_speed * Time.deltaTime);

        // Arm animation
        float s = Mathf.Sin(attack_progress * Mathf.PI);
        for (int i = 0; i < arms.Count; ++i)
        {
            var a = arms[i];
            var at = arm_targets[i];
            float amp = i % 2 == 0 ? s : 1 - s;

            Vector3 delta = c.transform.forward * (amp + 0.1f) + Vector3.up * (0.5f - amp) * 0.5f;
            if (delta.magnitude > 1f) delta /= delta.magnitude;

            at.position = a.shoulder.position + a.total_length * delta;
        }
    }

    public void on_end_control(character c)
    {
        foreach (var at in arm_targets)
            Object.Destroy(at.gameObject);
    }

    public string inspect_info() => "attacking";
    public void draw_gizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(target.position, 0.1f);
    }
    public void draw_inspector_gui() { }
}

public class chase_controller : ICharacterController
{
    chase_path path;
    int index;
    Transform chasing;
    IAcceptsDamage to_damage;
    attack_controller attack;

    public chase_controller(Transform chasing, IAcceptsDamage to_damage)
    {
        this.chasing = chasing;
        this.to_damage = to_damage;
    }

    public void control(character c)
    {
        if (attack == null)
        {
            if (c.distance_to(chasing) < c.attack_range)
            {
                // Close enough to start attacking
                attack = new attack_controller(c, chasing, to_damage);
                path = null;
            }
        }
        else
        {
            if (c.distance_to(chasing) > c.attack_range * 2f)
            {
                // Far enough to cancel attacking
                attack.on_end_control(c);
                attack = null;
            }
        }

        if (attack != null)
        {
            // I'm attacking
            attack.control(c);
            return;
        }

        if (path == null)
        {
            // Decrease allowed pathfinding iterations for closer targets 
            // (so we fail more quickly in hopeless pathing cases)
            int max_iter = (int)(c.transform.position - chasing.transform.position).magnitude;
            max_iter = Mathf.Min(500, 10 + max_iter * max_iter);

            float goal_distance = c.pathfinding_resolution * 0.8f;
            path = new chase_path(c.transform.position, chasing, c, max_iterations: max_iter, goal_distance: goal_distance);

            path.on_state_change_listener = (s) =>
            {
                if (s == global::path.STATE.COMPLETE || s == global::path.STATE.PARTIALLY_COMPLETE)
                {
                    // Ensure the path actually goes somewhere
                    if (path == null || path.length < 2)
                    {
                        path = null;
                        return;
                    }

                    // End right next to start => probably blocked by a wall
                    Vector3 delta = path[path.length - 1] - path[0];
                    if (delta.magnitude < c.pathfinding_resolution)
                        loose_interest(c);
                }
            };

            // Unstick the character if we fail to find a valid start point
            path.on_invalid_start = () => c?.unstuck();

            // No end found => loose interest
            path.on_invalid_end = () => loose_interest(c);

            index = 0;
        }

        switch (path.state)
        {
            case global::path.STATE.SEARCHING:
                path.pathfind(load_balancing.iter * 2);
                break;

            case global::path.STATE.FAILED:
                path = null;
                loose_interest(c);
                break;

            case global::path.STATE.COMPLETE:
            case global::path.STATE.PARTIALLY_COMPLETE:
                walk_path(c);
                break;

            default:
                Debug.LogError("Unkown path state in chase_controller!");
                break;
        }
    }

    void loose_interest(character c)
    {
        c.is_aware = false;
        c.controller = new default_character_control();
    }

    void walk_path(character c)
    {
        if (path.length <= index)
        {
            // Reached the end of the path, time for a new one
            path = null;
            return;
        }

        // Walk along the path
        if (c.move_towards(path[index], c.run_speed, out bool failed))
            ++index;

        if (failed) path = null;
    }

    public void on_end_control(character c) { }
    public void draw_gizmos()
    {
        if (attack != null)
        {
            attack.draw_gizmos();
            return;
        }

        path?.draw_gizmos();
    }
    public void draw_inspector_gui() { }
    public string inspect_info() => attack == null ? "Chasing" : "Attacking";
}

public class default_character_control : ICharacterController
{
    ICharacterController subcontroller;

    void check_for_player_interactions(character c)
    {
        foreach (var p in player.all)
        {
            if (p == null) continue;

            // Apply awareness modifications
            c.run_awareness_checks(p);
            if (!c.is_aware) continue; // Not yet aware

            switch (c.friendliness)
            {
                case character.FRIENDLINESS.FRIENDLY:
                    break; // Just wander around idly

                case character.FRIENDLINESS.AFRAID:
                    subcontroller = new flee_controller(p.transform);
                    break;

                case character.FRIENDLINESS.AGRESSIVE:
                    subcontroller = new chase_controller(p.transform, p);
                    break;
            }

            break;
        }
    }

    public void control(character c)
    {
        // If wandering idly, check for interactions with players
        if (subcontroller is idle_wander)
            check_for_player_interactions(c);

        // If character is not aware, return to wandering idly
        else if (subcontroller == null || !c.is_aware)
            subcontroller = new idle_wander();

        subcontroller.control(c);
    }

    public void on_end_control(character c) { subcontroller?.on_end_control(c); }
    public void draw_gizmos() { subcontroller?.draw_gizmos(); }
    public void draw_inspector_gui() { subcontroller?.draw_inspector_gui(); }
    public string inspect_info() { return subcontroller?.inspect_info(); }
}