using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_requirement_tracker : tutorial_object
{
    public RectTransform item_requirement_template;
    public UnityEngine.UI.Text hint_text;

    Dictionary<string, int> requirements;
    Dictionary<string, UnityEngine.UI.Text> text_trackers;

    public delegate void on_complete_func();
    on_complete_func on_complete;

    public bool is_complete
    {
        get => _is_complete;
        private set
        {
            if (_is_complete == value) return; // No change
            _is_complete = value;
            if (_is_complete)
            {
                on_complete?.Invoke();
                Destroy(gameObject);
            }
        }
    }
    bool _is_complete;

    public static item_requirement_tracker create(
        string hint_text, Dictionary<string, int> requirements,
        on_complete_func on_complete = null,
        bool show_recipe_book_hint = true,
        bool remove_already_in_inventory = true)
    {
        // Remove existing items if required
        if (remove_already_in_inventory && player.current?.inventory != null)
            foreach (var kv in requirements)
                player.current.inventory.remove_all(kv.Key);

        var ui = Resources.Load<RectTransform>("ui/item_requirement_tracker").inst(game.canvas.transform);
        ui.anchoredPosition = Vector2.zero;
        var ret = ui.GetComponent<item_requirement_tracker>();

        ret.requirements = requirements;
        ret.text_trackers = new Dictionary<string, UnityEngine.UI.Text>();
        ret.on_complete = on_complete;
        ret.hint_text.text = hint_text;

        if (show_recipe_book_hint)
            ret.hint_text.text += "\nThe recipe book can be opened by pressing " + controls.bind_name(controls.BIND.OPEN_RECIPE_BOOK) + ".";

        foreach (var kv in requirements)
        {
            var itm = Resources.Load<item>("items/" + kv.Key);
            var requirement = ret.item_requirement_template.inst(ret.item_requirement_template.parent);
            requirement.Find("sprite").GetComponent<UnityEngine.UI.Image>().sprite = itm.sprite;
            var txt = requirement.GetComponentInChildren<UnityEngine.UI.Text>();
            ret.text_trackers[kv.Key] = txt;
            txt.text = "0/" + kv.Value;
        }

        // Destroy template objects
        ret.item_requirement_template.SetParent(null);
        Destroy(ret.item_requirement_template.gameObject);

        ret.set_inventory_listener();
        ret.update_satisfaction();

        return ret;
    }

    void set_inventory_listener()
    {
        // Wait until the inventory is ready
        if (player.current == null) { Invoke("set_inventory_listener", 0.2f); return; }
        if (player.current.inventory == null) { Invoke("set_inventory_listener", 0.2f); return; }

        player.current.inventory.add_on_change_listener(update_satisfaction, () => this == null);
    }

    void update_satisfaction()
    {
        if (is_complete) return; // Already completed

        // Wait until the inventory is ready
        if (player.current == null) { Invoke("update_satisfaction", 0.2f); return; }
        if (player.current.inventory == null) { Invoke("update_satisfaction", 0.2f); return; }

        bool complete = true;
        foreach (var kv in requirements)
        {
            int req = requirements[kv.Key];
            int have = player.current.inventory.count(kv.Key);
            text_trackers[kv.Key].text = have + "/" + req;
            if (have < req) complete = false;
        }

        is_complete = complete;
    }
}
