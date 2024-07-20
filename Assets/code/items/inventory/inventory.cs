using System.Collections.Generic;
using UnityEngine;

public interface IItemCollection
{
    Dictionary<item, int> contents();
    bool add(item i, int count);
    bool remove(item i, int count);
}

// "Of course, that's the business we are in as a common language 
// runtime, but we haven't got around to doing it for MI yet"
public static class item_collection_extensions
{
    public static bool contains(this IItemCollection col, item i, int count = 1)
    {
        int found = 0;
        foreach (var kv in col.contents())
            if (kv.Key.name == i.name)
            {
                found += kv.Value;
                if (found >= count)
                    return true;
            }

        return false;
    }

    public static bool remove(this IItemCollection col, string item_name, int count)
    {
        return col.remove(Resources.Load<item>("items/" + item_name), count);
    }

    public static int remove_all(this IItemCollection col, item item)
    {
        int count = col.count(item);
        col.remove(item, count);
        return count;
    }

    public static int remove_all(this IItemCollection col, string item_name)
    {
        return col.remove_all(Resources.Load<item>("items/" + item_name));
    }

    public static bool add(this IItemCollection col, string item_name, int count)
    {
        return col.add(Resources.Load<item>("items/" + item_name), count);
    }

    public static int count(this IItemCollection col, item i)
    {
        var cts = col.contents();
        if (cts.TryGetValue(i, out int found))
            return found;
        return 0;
    }

    public static int total_item_count(this IItemCollection col)
    {
        int ret = 0;
        foreach (var kv in col.contents())
            ret += kv.Value;
        return ret;
    }

    public static item remove_first(this IItemCollection col)
    {
        foreach (var kv in col.contents())
        {
            if (col.remove(kv.Key, 1))
                return kv.Key;
            return null;
        }
        return null;
    }

    public static item get_first(this IItemCollection col)
    {
        foreach (var kv in col.contents())
            if (kv.Value > 0)
                return kv.Key;
        return null;
    }

    public static void clear(this IItemCollection col)
    {
        var cts = col.contents();
        foreach (var kv in cts)
            col.remove(kv.Key, kv.Value);
    }

    public static bool is_empty(this IItemCollection col)
    {
        foreach (var kv in col.contents())
            if (kv.Key != null && kv.Value > 0)
                return false;
        return true;
    }
}

/// <summary> A simple, dictionary-based
/// implementation of an item collection. </summary>
public class simple_item_collection : IItemCollection
{
    Dictionary<string, int> items = new Dictionary<string, int>();

    public bool empty => items.Count == 0;

    public Dictionary<item, int> contents()
    {
        Dictionary<item, int> ret = new Dictionary<item, int>();
        foreach (var kv in items)
            ret.Add(Resources.Load<item>("items/" + kv.Key), kv.Value);
        return ret;
    }

    public bool add(item i, int count)
    {
        if (i == null || count == 0) return true;
        if (items.ContainsKey(i.name))
            items[i.name] += count;
        else
            items[i.name] = count;
        return true;
    }

    public bool remove(item i, int count)
    {
        if (items.TryGetValue(i.name, out int found))
        {
            if (found > count)
            {
                items[i.name] -= count;
                return true;
            }
            items.Remove(i.name);
            return found == count;
        }
        else return false;
    }

    public string contents_string()
    {
        string ret = "";
        int i = 0;
        var cts = contents();
        foreach (var kv in cts)
        {
            if (kv.Value > 1) ret += kv.Value + " " + kv.Key.plural;
            else if (kv.Value == 1) ret += "1 " + kv.Key.display_name;
            if (i < cts.Count - 2) ret += ", ";
            else if (i == cts.Count - 2) ret += " and ";
            ++i;
        }
        return ret;
    }
}

public class inventory : networked, IItemCollection
{
    public RectTransform ui_prefab;

    // Static constructor
    static inventory()
    {
        help_book.add_entry("inventory", () =>
            "The inventory can be accessed by pressing " + controls.bind_name(controls.BIND.OPEN_INVENTORY) +
            "\n\nTo equip an item, it must be placed in the toolbar at the top of the inventory." +
            "\n\nIn order to craft, ingredients must be placed in the crafting area on the right hand side." +
            "\n\nItem stacks can be split in half by right clicking."
         );
    }

    void generate_ui()
    {
        // Create the ui element
        _ui = ui_prefab.inst(game.canvas.transform);
        _ui.anchoredPosition = Vector2.zero;

        // Setup the ui slots to link to this inventory
        _slots = _ui.GetComponentsInChildren<inventory_slot>();
        for (int i = 0; i < _slots.Length; ++i)
        {
            var isb = _slots[i].button.gameObject.AddComponent<inventory_slot_button>();
            isb.index = i;
            isb.inventory = this;

            // Initalize ui to empty
            _slots[i].update(null, 0, this);
        }

        // UI starts closed, is opened using the "open" set method
        _ui.gameObject.SetActive(false);
    }

    /// <summary> The ui element representing this inventory. </summary>
    public RectTransform ui
    {
        get
        {
            if (_ui == null) generate_ui();
            return _ui;
        }
    }
    RectTransform _ui;

    /// <summary> The UI slots representing this inventory. </summary>
    inventory_slot[] slots
    {
        get
        {
            if (_slots == null) generate_ui();
            return _slots;
        }
    }
    inventory_slot[] _slots;

    /// <summary> My child networked inventory slots. </summary>
    HashSet<inventory_slot_networked> networked_slots = new HashSet<inventory_slot_networked>();

    public override void on_add_networked_child(networked child)
    {
        if (child is inventory_slot_networked)
            networked_slots.Add(child as inventory_slot_networked);
    }

    public override void on_delete_networked_child(networked child)
    {
        if (child is inventory_slot_networked)
            networked_slots.Remove(child as inventory_slot_networked);
    }

    public inventory_slot_networked nth_slot(int n)
    {
        foreach (var isn in networked_slots)
            if (isn.index == n)
                return isn;
        return null;
    }

    /// <summary> Is the ui element currently active? </summary>
    public bool open
    {
        get => ui.gameObject.activeInHierarchy;
        set => ui.gameObject.SetActive(value);
    }

    /// <summary> Set the item/count in the corresponding slot. </summary>
    public void set_slot(inventory_slot s, string item, int count, bool overwrite = true)
    {
        int slot_index = -1;
        for (int i = 0; i < slots.Length; ++i)
            if (slots[i] == s)
            {
                slot_index = i;
                break;
            }

        if (slot_index < 0)
            throw new System.Exception("Slot not found in inventory!");

        // Look for a non-empty slot to overwrite
        foreach (var isn in networked_slots)
            if (isn.index == slot_index)
            {
                if (!overwrite) return;
                isn.set_item_count_index(Resources.Load<item>("items/" + item), count, slot_index);
                return;
            }

        // Create new networked slot
        var new_slot = (inventory_slot_networked)client.create(
            transform.position, "misc/networked_inventory_slot", this);
        new_slot.set_item_count_index(Resources.Load<item>("items/" + item), count, slot_index);
    }

    /// <summary> Forward a click to the appropriate network slot. </summary>
    public void click_slot(int slot_index, bool right_click)
    {
        // Ensure the ui exists
        if (ui == null)
            throw new System.Exception("UI should create itself!");

        var mi = FindObjectOfType<mouse_item>();

        foreach (var isn in networked_slots)
            if (isn.index == slot_index)
            {
                if (mi != null)
                {
                    if (isn.item_name == mi.item.name)
                    {
                        if (right_click)
                        {
                            // Add 1 of the mouse item to the slot
                            isn.add(mi.item, 1);
                            mi.count--;
                        }
                        else
                        {
                            // Add the mouse item to the slot
                            isn.add(mi.item, mi.count);
                            mi.count = 0;
                        }
                    }
                    else if(!right_click)
                    {
                        // Switch the mouse item with that in the slot
                        if (slots[slot_index].accepts(mi.item))
                        {
                            item to_pickup = isn.item;
                            int quantity = isn.count;
                            isn.set_item_count_index(mi.item, mi.count, slot_index);
                            mi.count = 0;
                            mouse_item.create(to_pickup, quantity, this);
                        }
                    }

                    return;
                }
                else
                {
                    // Move the contents of the slot directly into the player inventory
                    // (or from the player inventory to the interacting inventory)
                    if (controls.held(controls.BIND.QUICK_ITEM_TRANSFER))
                    {
                        // Copies for lambda function
                        item transfer_item = isn.item;
                        int transfer_count = isn.count;
                        int transfer_slot = slot_index;

                        // Transfer into player inventory, or out of player inventory
                        inventory target = player.current.inventory;
                        if (this == player.current.inventory)
                        {
                            // Transfer out of player inventory, prioritising
                            // left_menu inventory, then the player crafting menu.
                            target = player.current.interactions?.editable_inventory();
                            if (target == null) target = player.current.crafting_menu;
                        }

                        if (target != null && target.can_add(transfer_item, transfer_count))
                        {
                            isn.set_item_count_index(null, 0, isn.index);
                            isn.delete(() =>
                            {
                                // Transfer into target inventory
                                target.add(transfer_item, transfer_count, popup_if_player: false);
                            });
                        }

                        return;
                    }

                    // Pickup the mouse item from the slot
                    isn.pickup(right_click);
                    return;
                }
            }

        // If we've got here => the slot is not yet networked => it's empty
        if (mi != null && slots[slot_index].accepts(mi.item))
        {
            // We're putting the item(s) in this slot
            // Create a networked slot with the corresponding info
            var isn = (inventory_slot_networked)client.create(
                transform.position, "misc/networked_inventory_slot", this);
            if (right_click)
            {
                isn.set_item_count_index(mi.item, 1, slot_index);
                mi.count--;
            }
            else
            {
                isn.set_item_count_index(mi.item, mi.count, slot_index);
                mi.count = 0;

            }
        }
    }

    public bool can_add(string item, int count)
    {
        var to_add = Resources.Load<item>("items/" + item);
        if (to_add == null) throw new System.Exception("Could not find the item " + item);
        return can_add(to_add, count);
    }

    public bool can_add(item item, int count)
    {
        if (item == null || count == 0)
            return true;

        // Ensure we're adding the prefab version of this item
        item = Resources.Load<item>("items/" + item.name);

        // See if a slot for this item already exists
        var networked_indicies = new HashSet<int>();
        foreach (var isn in networked_slots)
        {
            if (isn.item_name == item.name) return true;
            networked_indicies.Add(isn.index);
        }

        // See if there is a free slot to put the item in
        for (int i = 0; i < slots.Length; ++i)
        {
            if (networked_indicies.Contains(i)) continue; // This slot is taken
            if (slots[i].accepts(item))
                return true;
        }

        return false;
    }

    public bool add(string item, int count)
    {
        var to_add = Resources.Load<item>("items/" + item);
        if (to_add == null) throw new System.Exception("Could not find the item " + item);
        return add(to_add, count);
    }

    public bool add(item item, int count) => add(item, count, popup_if_player: true);

    public bool add(item item, int count, bool popup_if_player = true)
    {
        if (item == null || count == 0)
            return true;

        // Ensure we're adding the prefab version of the item
        item = Resources.Load<item>("items/" + item.name);

        bool added = false;

        // Attempt to add the item to existing networked slots
        var networked_indicies = new HashSet<int>();
        foreach (var isn in networked_slots)
        {
            if (isn.add(item, count))
            {
                added = true;
                break;
            }
            networked_indicies.Add(isn.index);
        }

        if (!added)
        {
            // Find an empty slot to add the item to
            for (int i = 0; i < slots.Length; ++i)
            {
                if (networked_indicies.Contains(i)) continue; // This slot is taken
                if (slots[i].accepts(item))
                {
                    // Create a networked slot with the corresponding info
                    var isn = (inventory_slot_networked)client.create(
                        transform.position, "misc/networked_inventory_slot", this);
                    isn.set_item_count_index(item, count, i);
                    added = true;
                    break;
                }
            }
        }

        // If this is the local player inventory, display a message on success
        if (popup_if_player && added && GetComponentInParent<player>() == player.current)
        {
            int total = 0;
            contents().TryGetValue(item, out total);
            string msg = "+ " + count.qs() + " " +
                         (count > 1 ? item.plural : item.display_name) +
                         " (" + total + ")";
            popup_message.create(msg);
        }

        return added;
    }

    public bool remove(string item, int count)
    {
        var to_remove = Resources.Load<item>("items/" + item);
        if (to_remove == null) throw new System.Exception("Could not find the item " + item);
        return remove(to_remove, count);
    }

    public bool remove(item item, int count)
    {
        // Tried to remove nothing, always succeeds
        if (item == null || count == 0)
            return true;

        // Ensure we're removing the prefab version of the item
        // and that we have enough of the item to remove
        item = Resources.Load<item>("items/" + item.name);
        if (!contains(item.name, count)) return false;

        // Run over the occupied (networked) slots, and remove count items
        foreach (var isn in new List<inventory_slot_networked>(networked_slots))
        {
            count -= isn.remove(item, count);
            if (count <= 0) break;
        }

        if (count != 0)
            throw new System.Exception("Items not removed properly!");

        // Removal successful
        return true;
    }

    public bool contains(string item, int count = 1)
    {
        var to_test = Resources.Load<item>("items/" + item);
        if (to_test == null) throw new System.Exception("Could not find the item " + item);
        return item_collection_extensions.contains(this, to_test, count);
    }

    public inventory_slot_networked find_slot_by_item(item item)
    {
        if (item == null) return null;
        foreach (var isn in networked_slots)
            if (isn.item_name == item.name)
                return isn;
        return null;
    }

    public bool empty
    {
        get
        {
            foreach (var isn in networked_slots)
                if (isn.item != null && isn.count > 0)
                    return false;
            return true;
        }
    }

    public Dictionary<item, int> contents()
    {
        Dictionary<item, int> ret = new Dictionary<item, int>();
        foreach (var isn in networked_slots)
        {
            if (isn == null) continue; // Destroyed
            if (isn.item == null) continue; // No item
            if (isn.count < 1) continue; // No quantity

            // Add the contents to the dictionary
            if (!ret.ContainsKey(isn.item)) ret[isn.item] = isn.count;
            else ret[isn.item] += isn.count;
        }
        return ret;
    }

    public int count(string item)
    {
        return item_collection_extensions.count(this,
            Resources.Load<item>("items/" + item));
    }

    struct change_listener_info
    {
        public remove_func remove_func;
        public on_change_func callback;
    }

    public delegate void on_change_func();
    public delegate bool remove_func();

    HashSet<change_listener_info> listeners = new HashSet<change_listener_info>();
    public void add_on_change_listener(on_change_func f, remove_func remove_listener_delegate = null)
    {
        listeners.Add(new change_listener_info
        {
            remove_func = remove_listener_delegate,
            callback = f
        });
    }

    /// <summary> Call to invoke listeners added via <see cref="add_on_change_listener(on_change_func)"/>. </summary>
    public void invoke_on_change()
    {
        foreach (var f in new List<change_listener_info>(listeners))
        {
            if (f.remove_func != null && f.remove_func())
            {
                listeners.Remove(f);
                continue;
            }

            f.callback?.Invoke();
        }
    }

    public int last_frame_changed { get; private set; }

    /// <summary> Called when an <see cref="inventory_slot_networked"/> changes contents. </summary>
    public void on_slot_change(int slot_index, item item, int count)
    {
        slots[slot_index].update(item, count, this);
        last_frame_changed = Time.frameCount;
        invoke_on_change();
    }
}
