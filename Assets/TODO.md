# TODO

- Work out the exact swing-path of melee weapons in order to interpolate and detect collisions
- Optimize chase paths
 don't double back if thats what the player did
 Make characters fall if they are following a chase path over a cliff
 Figure out how to stop enemies bunching up when chasing
- Point-to-point connection building (e.g ropes for a rope bridge)
- Remove the off-by-one thing happenng with player.slot_equipped (not a bug, just
  at some points in the code is indexed from 0 and others from 1, which is mildly annoying).
- Make it more obvious which side of a ladder is the front?
- Make time command work from non-auth clients
- PvP hit markers
- Update all the on_ methods in the networked class to be listeners that can be +='d (big job, but would
  allow the simplification of the code in a lot of places) - does the new callbacks structure of
  IExtendsNetworked cover these use cases?
- town_path_element.path spread load across multiple frames.
- Settlers heads move over to their shoulder with time
- Add cowering spots to various things (beds)
- Recreational tasks
- Make trade carts actually move
- Stop menus persisting when the object they refer to has been deleted (done?)
- Remove contract system?
- Polar bears fall through floor on death/spawn underwater
- generated trading camps
- Tavern
- Manual fishing
- Rail network
- Hospital
- Potions
- max simulataneous researchers
- Setters sad if working uncovered
- Make chest into an attachable component
- Buildings to speed up research
- Mineshaftable-locations should be inspectable
- Tiers/different kinds of arboretum? - Fuel supply is often via arboreta
- Maybe make attacks more frequent, so guards are a full-time job, but don't require the entire town?
- Version of dining table where settlers choose food according to nutrition requirements
- Limit on number of items market stall can sell (improved versions can sell more?)
- Global scroll speed controls
- Subtutorials - the below should not really be order-dependant:
  - Tutorial step for dining table (triggers when the player first makes one)
  - Tutorial step for breadmaking (triggers when the player first researches breadmaking)
- More foliage? (small plants/flowers)
- Buildings take a wider variety of materials (too many plank+log+x buildings -
  would help with crafting menu clutter/introduce progression)
- Town name markers when far away
- Debug save+exit/re-enter game
- Make it so all building stages in the tutorial have a hint about the recipe book
- Add the recipe book to the ESC menu
- Add a help entry about the recipe book
- Inverted mouse y option
