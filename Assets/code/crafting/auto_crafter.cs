﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A building material that can automatically craft recipes via
/// the item logistics system. Can also be used by hand. </summary>
public class auto_crafter : building_material, IPlayerInteractable
{
    public float craft_time = 1f;
    public AudioClip custom_crafting_sound;
    public float custom_crafting_sound_volume = 1f;
    public List<GameObject> enable_when_crafting = new List<GameObject>();

    float crafting_time_left = -1f;

    recipe[] recipies;

    item_input[] inputs => GetComponentsInChildren<item_input>();
    item_output[] outputs => GetComponentsInChildren<item_output>();

    recipe.checklist ingredients
    {
        get
        {
            if (_ingredients == null)
                _ingredients = new recipe.checklist(recipies[chosen_recipe.value]);
            return _ingredients;
        }
        set => _ingredients = value;
    }
    recipe.checklist _ingredients;

    void process_inputs()
    {
        foreach (var e in enable_when_crafting)
            e.SetActive(false);

        // Accept new inputs that complete the recipe
        foreach (var ip in inputs)
        {
            // No items at this input
            if (ip.item_count == 0) continue;

            // Attempt to complete the recipe with the item from this input
            switch (ingredients.try_check_off(ip.peek_next_item()))
            {
                // Recipe is complete, wait until ingredients
                // have been used to pass judgement
                case recipe.checklist.CHECK_OFF_RESULT.ALREADY_COMPLETE:
                    break;

                // Input was used to partially complete the recipe
                // => suck it into the machine
                case recipe.checklist.CHECK_OFF_RESULT.ADDED:
                    Destroy(ip.release_next_item().gameObject);
                    break;

                // Input was used to complete the recipe
                // => suck it into the machine and mark recipe as complete
                case recipe.checklist.CHECK_OFF_RESULT.ADDED_AND_COMPLETED:
                    Destroy(ip.release_next_item().gameObject);
                    crafting_time_left = craft_time;
                    break;

                // This input isn't needed right now, so leave it there
                // for now, but replace it if something else comes along
                case recipe.checklist.CHECK_OFF_RESULT.NOT_NEEDED_RIGHT_NOW:
                    ip.set_replace_next();
                    break;

                // All other cases - reject the item
                default:
                    item_rejector.create(ip.release_next_item());
                    break;
            }
        }
    }

    void continue_crafting()
    {
        foreach (var e in enable_when_crafting)
            e.SetActive(true);

        // Continue crafting
        crafting_time_left -= Time.deltaTime;
        if (crafting_time_left > 0) return; // Crafting not complete

        // Crafting success
        ingredients.craft_to(outputs[0], track_production: true);
        audio_source.Play();
    }

    AudioSource audio_source
    {
        get
        {
            if (_audio_source == null)
            {
                _audio_source = new GameObject("audio_source").AddComponent<AudioSource>();
                _audio_source.transform.SetParent(transform);
                _audio_source.transform.localPosition = Vector3.zero;
                _audio_source.spatialBlend = 1f; // 3D
                _audio_source.clip = custom_crafting_sound;
                _audio_source.volume = custom_crafting_sound_volume;
            }
            return _audio_source;
        }
    }
    AudioSource _audio_source;

    private void Update()
    {
        // If crafting_time_left >= 0, then we are crafting
        // otherwise, we are processing inputs.
        if (crafting_time_left < 0) process_inputs();
        else continue_crafting();
    }

    //############//
    // NETWORKING //
    //############//

    // Save the chosen recipe
    networked_variables.net_int chosen_recipe;

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();

        // Load the recipes
        recipies = Resources.LoadAll<recipe>("recipes/autocrafters/" + name);

        chosen_recipe = new networked_variables.net_int();

        chosen_recipe.on_change = () =>
        {
            ingredients = new recipe.checklist(recipies[chosen_recipe.value]);
        };
    }

    protected override recover_settings_func get_recover_func()
    {
        int recipe_copy = chosen_recipe.value;
        return (c) =>
        {
            // Get the copied autocrafter
            var a = c as auto_crafter;
            if (a == null)
            {
                Debug.LogError("Tried to recover autocrafter recipe on non-autocrafter!");
                return;
            }

            // Recover the recipe (once registered)
            a.add_register_listener(() =>
            {
                a.chosen_recipe.value = recipe_copy;
            });
        };
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    player_interaction[] interactions;
    public override player_interaction[] player_interactions(RaycastHit hit)
    {
        if (is_logistics_version) return base.player_interactions(hit);
        if (interactions == null) interactions = base.player_interactions(hit).prepend(
            new menu(this),
            new player_inspectable(transform)
            {
                sprite = () => sprite,
                text = () =>
                {
                    string info = display_name.capitalize() + "\n";

                    if (crafting_time_left > 0)
                    {
                        info += "Crafting " +
                            product.product_quantities_list(ingredients.recipe.products) + " from " +
                            ingredients.stored.contents_string();
                        float completion = 100f * (1f - crafting_time_left / craft_time);
                        info += " (" + completion.ToString("F0") + "%)";
                    }
                    else if (!ingredients.stored.empty)
                    {
                        info += "Contents: " + ingredients.stored.contents_string();
                    }

                    return info.Trim();
                }
            });
        return interactions;
    }

    class menu : left_player_menu
    {
        auto_crafter crafter;
        crafting_entry[] recipe_buttons;

        public menu(auto_crafter crafter) : base(crafter.display_name) { this.crafter = crafter; }

        public override recipe[] additional_recipes(out string name, out AudioClip crafting_sound, out float crafting_sound_vol)
        {
            name = crafter.display_name;
            crafting_sound = crafter.custom_crafting_sound;
            crafting_sound_vol = crafter.custom_crafting_sound_volume;
            return crafter.recipies;
        }

        protected override void on_open()
        {
            base.on_open();

            // Simulate a click on the initially-selected button
            if (crafter.chosen_recipe.value >= 0 && crafter.chosen_recipe.value < recipe_buttons.Length)
                recipe_buttons[crafter.chosen_recipe.value].button.onClick.Invoke();
        }

        protected override bool should_close_now()
        {
            // Crafter has been destroyed
            return crafter == null;
        }

        protected override RectTransform create_menu(Transform parent)
        {
            if (crafter.outputs.Length == 0 || crafter.inputs.Length == 0)
                return null;

            recipe_buttons = new crafting_entry[crafter.recipies.Length];

            var left_menu = Resources.Load<RectTransform>("ui/autocrafter").inst(parent);
            var content = left_menu.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;

            for (int i = 0; i < crafter.recipies.Length; ++i)
            {
                // Create the recipe selection button
                recipe_buttons[i] = crafter.recipies[i].get_entry(parent);
                var button_i = recipe_buttons[i];
                button_i.transform.SetParent(content);

                // Copies for lambda function
                int i_copy = i;
                var reset_colors = button_i.button.colors;

                button_i.button.onClick.AddListener((() =>
                {
                    if (controls.held(controls.BIND.QUICK_ITEM_TRANSFER))
                    {
                        // Transfer the recipe ingredients to the crafting menu
                        if (player.current == null) return;

                        var r = crafter.recipies[i_copy];
                        bool can_craft = r.can_craft(player.current.inventory, out Dictionary<string, int> to_use);
                        foreach (var kv in to_use)
                        {
                            if (player.current.inventory.remove(kv.Key, kv.Value))
                                player.current.crafting_menu.add(kv.Key, kv.Value);
                        }
                        return;
                    }

                    crafter.chosen_recipe.value = i_copy;

                    // Update colors to highlight selection
                    for (int j = 0; j < crafter.recipies.Length; ++j)
                    {
                        var button_j = recipe_buttons[j];
                        var colors = button_j.button.colors;
                        if (j == i_copy)
                        {
                            // Selected recipe
                            colors.normalColor = Color.green;
                            colors.pressedColor = Color.green;
                            colors.highlightedColor = Color.green;
                            colors.selectedColor = Color.green;
                            colors.disabledColor = Color.green;
                        }
                        else if (!crafter.recipies[j].unlocked)
                        {
                            // Locked recipe
                            colors.normalColor = Color.grey;
                            colors.pressedColor = Color.grey;
                            colors.highlightedColor = Color.grey;
                            colors.selectedColor = Color.grey;
                            colors.disabledColor = Color.grey;
                        }
                        else colors = reset_colors;
                        button_j.button.colors = colors;
                        button_j.button.interactable = crafter.recipies[j].unlocked;
                    }
                }));
            }

            return left_menu;
        }
    }
}
