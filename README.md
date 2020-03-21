# Pokémon Shuffler

A Pokémon randomizer for NDS and higher games (generation IV and up).

Currently it doesn't do much at all. Randomization goals:

* wild pokémon
* trainer pokémon
  * optionally type-themed
* color palettes
  * pokémon
  * pokéballs
  * trainer sprites
  * buildings
* learnsets
* level-ups
  * Allow for a random number of level-up methods (instead of just shuffling existing level-ups)
  * Interesting level-up modes: monophyletic, monophyletic types, type-consistent
* trainer classes
* trainer names
  * with consistency in dialogue
* gender ratios
* types
* trainer sprite / model (swap with NPC?)

Quality of life goals:

* remove trade level-ups
* remove "while knowing move X" level-ups
* generate a guide to the pokémon game that shows where pokémon can be found, how to level them up, what moves they learn, etc
  * something you can search through, like "what can I find on Route 4?" and "how can I get a bulbasaur?"
  * include gym leader info
  * control spoiler levels (with allowed spoiler level baked into the ROM)
* include the randomization parameters in the ROM itself, and allow them to be accessed
  * ideally in-game, eg by modifying your trainer card

Silliness:

* silly trainer class names
* silly dialogue

We'll have both command line and GUI versions.
