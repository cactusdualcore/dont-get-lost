﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler : character, IPlayerInteractable, ICanEquipArmour
{
    public const float HUNGER_PER_SECOND = 0.2f;
    public const float HEAL_RATE = 100f / 120f;
    public const float TIREDNESS_PER_SECOND = 100f / time_manager.DAY_LENGTH;
    public const byte MAX_METABOLIC_SATISFACTION_TO_EAT = 220;
    public const byte GUARANTEED_EAT_METABOLIC_SATISFACTION = 64;
    public const int XP_PER_LEVEL = 1000;

    public List<Renderer> skin_renderers = new List<Renderer>();
    public List<Renderer> top_underclothes = new List<Renderer>();
    public List<Renderer> bottom_underclothes = new List<Renderer>();

    public Transform left_hand { get; private set; }
    public Transform right_hand { get; private set; }

    //########//
    // SKILLS //
    //########//

    public enum SKILL
    {
        // Please append new skills to the end of the
        // list, so it doesn't fuck up serialized values.
        // They should also retain the default values, as
        // they are used to index arrays.
        COOKING,
        CARPENTRY,
        FARMING,
        WOODCUTTING,
        MELEE_COMBAT,
        RANGED_COMBAT,
        MINING,
        SOCIAL,
    }

    public static SKILL[] all_skills => (SKILL[])System.Enum.GetValues(typeof(SKILL));
    public static string skill_name(SKILL s) => s.ToString().Replace("_", " ").ToLower().capitalize();

    //###################//
    // CHARACTER CONTROL //
    //###################//

    protected override ICharacterController default_controller() { return new settler_control(); }

    class settler_control : ICharacterController
    {
        public void control(character c)
        {
            var s = (settler)c;
            s.default_control();
        }

        public void on_end_control(character c) { }
        public void draw_gizmos() { }
        public void draw_inspector_gui() { }

        public string inspect_info()
        {
            return "";
        }
    }

    settler_path_element.path path;

    /// <summary> The path element that I am currently moving 
    /// towards. </summary>
    settler_path_element path_element
    {
        get => _path_element;
        set
        {
            if (_path_element == value)
                return;

            _path_element?.on_settler_leave(this);
            _path_element = value;
            _path_element?.on_settler_enter(this);
        }
    }
    settler_path_element _path_element;

    public int group => path_element == null ? -1 : path_element.group;
    public int room => path_element == null ? -1 : path_element.room;
    float delta_hunger = 0;
    float delta_tired = 0;
    float delta_heal = 0;
    float delta_xp = 0;
    settler_task_assignment assignment;

    void default_control()
    {
        // Don't do anything if there is a player interacting with me
        if (players_interacting_with.value > 0)
            return;

        // Look for my current assignment
        assignment = settler_task_assignment.current_assignment(this);

        if (!has_authority)
        {
            if (assignment != null)
            {
                // Mimic assignment on non-authority client
                if ((transform.position - assignment.transform.position).magnitude < 0.5f)
                    assignment.interactable.on_interact(this);
            }
            return;
        }

        // Authority-only control from here

        // Get hungry/tired/heal
        delta_hunger += HUNGER_PER_SECOND * Time.deltaTime;
        delta_tired += TIREDNESS_PER_SECOND * Time.deltaTime;

        delta_heal += HEAL_RATE * Time.deltaTime;

        if (delta_hunger > 1f)
        {
            delta_hunger = 0f;
            nutrition.modify_every_satisfaction(-1);
        }

        if (delta_tired > 1f)
        {
            delta_tired = 0f;
            tiredness.value += 1;
        }

        if (delta_heal > 1f)
        {
            delta_heal = 0f;
            heal(1);
        }

        // Look for a new assignment if I don't have one
        if (assignment == null)
        {
            // Reset stuff
            path = null;
            delta_xp = 0;

            // Attempt to find an interaction - go through job types in priority order
            for (int i = 0; i < job_priorities.ordered.Length; ++i)
            {
                if (Random.Range(0, 2) == 0) continue; // Add some randomness
                var j = job_priorities.ordered[i];
                if (!job_enabled_state[j]) continue; // Job type disabled
                var job = settler_interactable.proximity_weighted_ramdon(j, transform.position);
                if (settler_task_assignment.try_assign(this, job))
                    return;
            }

            // No suitable interaction found
            return;
        }

        // Wait for assignment to be registered
        if (assignment.network_id < 0)
            return;

        // We have an assignment, attempt to carry it out

        // Check if we have a path
        if (path == null)
        {
            // Find a path to the assignment
            path_element = settler_path_element.nearest_element(transform.position);
            path = new settler_path_element.path(path_element, assignment.interactable.path_element(group));

            if (path == null)
            {
                // Couldn't path to assignment, delete it
                assignment.delete();
                return;
            }
        }

        // Check if there is any of the path left to walk
        if (path.Count > 0)
        {
            settler_path_element element_walking_towards;
            if (path.walk(transform, assignment.interactable.move_to_speed(this), out element_walking_towards))
                path = null;
            path_element = element_walking_towards;
            path_element?.on_settler_move_towards(this);
            return;
        }

        // Carry out the assignment
        delta_xp += Time.deltaTime;
        if (delta_xp > 1f)
        {
            delta_xp = 0;
            foreach (var s in assignment.interactable.job.relevant_skills)
                skills[s] += Random.Range(0, 100); // Random so xp doesnt just stay in fixed intervals
        }

        switch (assignment.interactable.on_interact(this))
        {
            case settler_interactable.INTERACTION_RESULT.COMPLETE:
            case settler_interactable.INTERACTION_RESULT.FAILED:
                // Remove my assignment
                assignment.delete();
                return;
        }
    }

    protected override void on_death()
    {
        temporary_object.create(60f).gameObject.add_pinned_message("The settler " + name + " died!", Color.red);
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    private void Start()
    {
        // Get my left/right hand transforms
        foreach (var al in GetComponentsInChildren<armour_locator>())
            if (al.location == armour_piece.LOCATION.HAND)
            {
                switch (al.handedness)
                {
                    case armour_piece.HANDEDNESS.LEFT:
                        left_hand = al.transform;
                        break;

                    case armour_piece.HANDEDNESS.RIGHT:
                        right_hand = al.transform;
                        break;

                    case armour_piece.HANDEDNESS.EITHER:
                        throw new System.Exception("A hand has EITHER handedness!");
                }
            }

        settlers.Add(this);
    }

    private void OnDestroy()
    {
        path_element = null;
        settlers.Remove(this);
    }

    private void OnDrawGizmos()
    {
        if (path_element == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(path_element.transform.position, 0.1f);
    }

    //#################//
    // ICanEquipArmour //
    //#################//

    public armour_locator[] armour_locators() { return GetComponentsInChildren<armour_locator>(); }
    public float armour_scale() { return height.value; }
    public Color hair_color() { return net_hair_color.value; }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    player_interaction[] interactions;
    public override player_interaction[] player_interactions()
    {
        if (interactions == null) interactions = new player_interaction[]
        {
            new left_menu(this),
            new task_management(this),
            new player_inspectable(transform)
            {
                text = () =>
                {
                    return name.capitalize() + "\n" +
                       "    " + "Health " + remaining_health + "/" + max_health + "\n" +
                       "    " + Mathf.Round(tiredness.value) + "% tired\n" +
                       "    " + Mathf.Round(nutrition.hunger * 100f / 255f) + "% hungry\n" +
                       assignement_info() + "\n" +
                       "    interacting with " + players_interacting_with.value + " players";
                }
            }
        };
        return interactions;
    }

    class left_menu : left_player_menu
    {
        color_selector hair_color_selector;
        color_selector top_color_selector;
        color_selector bottom_color_selector;
        UnityEngine.UI.Text info_panel_text;
        settler settler;

        public left_menu(settler settler) : base(settler.name) { this.settler = settler; }
        public override inventory editable_inventory() { return settler.inventory; }
        protected override RectTransform create_menu() { return settler.inventory.ui; }
        protected override void on_open()
        {
            settler.players_interacting_with.value += 1;

            foreach (var cs in settler.inventory.ui.GetComponentsInChildren<color_selector>(true))
            {
                if (cs.name.Contains("hair")) hair_color_selector = cs;
                else if (cs.name.Contains("top")) top_color_selector = cs;
                else bottom_color_selector = cs;
            }

            foreach (var tex in settler.inventory.ui.GetComponentsInChildren<UnityEngine.UI.Text>())
                if (tex.name == "info_panel_text")
                {
                    info_panel_text = tex;
                    break;
                }

            info_panel_text.text = left_menu_text();

            hair_color_selector.color = settler.net_hair_color.value;
            top_color_selector.color = settler.top_color.value;
            bottom_color_selector.color = settler.bottom_color.value;

            hair_color_selector.on_change = () => settler.net_hair_color.value = hair_color_selector.color;
            top_color_selector.on_change = () => settler.top_color.value = top_color_selector.color;
            bottom_color_selector.on_change = () => settler.bottom_color.value = bottom_color_selector.color;
        }

        protected override void on_close()
        {
            settler.players_interacting_with.value -= 1;
        }

        string left_menu_text()
        {
            return settler.name.capitalize() + "\n\n" +
                   "Health " + settler.remaining_health + "/" + settler.max_health + "\n" +
                   settler.tiredness.value + "% tired\n" +
                   "Group " + settler.group + " room " + settler.room + "\n\n" +
                   settler.assignement_info() + "\n\n" +
                   settler.nutrition_info();
        }
    }

    class task_management : player_interaction
    {
        task_manager ui;
        settler settler;
        public task_management(settler settler) { this.settler = settler; }
        public override controls.BIND keybind => controls.BIND.OPEN_TASK_MANAGER;
        public override string context_tip() { return "manage tasks"; }
        public override bool allows_mouse_look() { return false; }
        public override bool allows_movement() { return false; }

        public override bool start_interaction(player player)
        {
            if (ui == null)
            {
                //Create the ui
                ui = Resources.Load<task_manager>("ui/task_manager").inst();
                ui.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
                ui.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                ui.set_target(settler);
                ui.open = false; // ui starts closed
            }

            // Open the ui
            ui.open = true;
            settler.players_interacting_with.value += 1;
            return false;
        }

        public override bool continue_interaction(player player)
        {
            return controls.triggered(keybind);
        }

        public override void end_interaction(player player)
        {
            // Close the ui
            settler.players_interacting_with.value -= 1;
            ui.open = false;
        }
    }

    //#######################//
    // Formatted information //
    //#######################//

    public string assignement_info()
    {
        string ass_string = "No assignment.";
        if (assignment != null)
            ass_string = "Assignment: " + assignment.interactable.task_info();
        return ass_string;
    }

    public string nutrition_info()
    {
        int max_length = 0;
        foreach (var g in food.all_groups)
            if (food.group_name(g).Length > max_length)
                max_length = food.group_name(g).Length;

        string ret = Mathf.Round(nutrition.hunger * 100f / 255f) + "% hungry\n";
        ret += "Diet satisfaction\n";
        foreach (food.GROUP g in food.all_groups)
        {
            string name = food.group_name(g).capitalize();
            while (name.Length < max_length) name += " ";
            ret += "  " + name + " " + Mathf.Round(nutrition[g] * 100f / 255f) + "%\n";
        }

        return ret;
    }

    //############//
    // NETWORKING //
    //############//

    public networked_variables.net_food_satisfaction nutrition;
    public networked_variables.net_int tiredness;
    public networked_variables.net_string net_name;
    public networked_variables.net_bool male;
    public networked_variables.net_int players_interacting_with;
    public networked_variables.net_color skin_color;
    public networked_variables.net_color top_color;
    public networked_variables.net_color net_hair_color;
    public networked_variables.net_color bottom_color;
    public networked_variables.net_job_priorities job_priorities;
    public networked_variables.net_job_enable_state job_enabled_state;
    public networked_variables.net_skills skills;
    new public networked_variables.net_float height;

    public override float position_resolution() { return 0.1f; }
    public override float position_lerp_speed() { return 2f; }
    public override bool persistant() { return true; }

    public inventory inventory { get; private set; }

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        nutrition = networked_variables.net_food_satisfaction.fully_satisfied;
        tiredness = new networked_variables.net_int(min_value: 0, max_value: 100);
        net_name = new networked_variables.net_string();
        male = new networked_variables.net_bool();
        skin_color = new networked_variables.net_color();
        top_color = new networked_variables.net_color();
        bottom_color = new networked_variables.net_color();
        net_hair_color = new networked_variables.net_color();
        height = new networked_variables.net_float();
        players_interacting_with = new networked_variables.net_int();
        job_priorities = new networked_variables.net_job_priorities();
        job_enabled_state = new networked_variables.net_job_enable_state();
        skills = new networked_variables.net_skills();

        net_name.on_change = () => name = net_name.value;

        skin_color.on_change = () =>
        {
            foreach (var r in skin_renderers)
                utils.set_color(r.material, skin_color.value);
        };

        top_color.on_change = () =>
        {
            foreach (var r in top_underclothes)
                utils.set_color(r.material, top_color.value);
        };

        bottom_color.on_change = () =>
        {
            foreach (var r in bottom_underclothes)
                utils.set_color(r.material, bottom_color.value);
        };

        net_hair_color.on_change = () =>
        {
            // Set hair color
            foreach (var al in armour_locators())
                if (al.equipped != null && al.equipped is hairstyle)
                    al.equipped.on_equip(this);
        };

        height.on_change = () =>
        {
            transform.localScale = Vector3.one * height.value;
            base.height = height.value * 1.5f + 0.2f;
        };
    }

    public override void on_create()
    {
        players_interacting_with.value = 0;
    }

    public override void on_first_create()
    {
        base.on_first_create();
        male.value = Random.Range(0, 2) == 0;
        if (male.value) net_name.value = names.random_male_name();
        else net_name.value = names.random_female_name();
        skin_color.value = character_colors.random_skin_color();
        top_color.value = Random.ColorHSV();
        bottom_color.value = Random.ColorHSV();
        net_hair_color.value = Random.ColorHSV();
        height.value = Random.Range(0.8f, 1.2f);

        foreach (var s in all_skills)
            skills[s] = Random.Range(0, 10 * XP_PER_LEVEL);
    }

    float armour_location_fill_probability(armour_piece.LOCATION loc)
    {
        switch (loc)
        {
            case armour_piece.LOCATION.HEAD: return 0.9f;
            default: return 0.25f;
        }
    }

    public override void on_first_register()
    {
        base.on_first_register();
        var inv = (inventory)client.create(transform.position, "inventories/settler_inventory", parent: this);

        // Randomize clothing
        inv.add_register_listener(() =>
        {
            var armour_slots = inv.ui.GetComponentsInChildren<armour_slot>();

            // Choose the armour slots to fill
            var locations_to_fill = new HashSet<armour_piece.LOCATION>();
            foreach (var slot in armour_slots)
                if (Random.Range(0, 1f) < armour_location_fill_probability(slot.location))
                    locations_to_fill.Add(slot.location);

            // Fill the chosen armour slots
            var armours = Resources.LoadAll<armour_piece>("items");
            foreach (var slot in armour_slots)
            {
                if (!locations_to_fill.Contains(slot.location))
                    continue;

                List<armour_piece> options = new List<armour_piece>();
                foreach (var a in armours)
                    if (slot.accepts(a))
                        options.Add(a);

                if (options.Count == 0)
                    continue;

                var chosen = options[Random.Range(0, options.Count)];
                inv.set_slot(slot, chosen.name, 1);
            }
        });
    }

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);
        if (child is inventory)
            inventory = (inventory)child;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<settler> settlers;

    public static int settler_count => settlers.Count;

    new public static void initialize()
    {
        settlers = new HashSet<settler>();
    }

    public static settler find_to_min(utils.float_func<settler> f)
    {
        return utils.find_to_min(settlers, f);
    }

    new public static string info()
    {
        return "    Total settler count : " + settlers.Count;
    }
}
