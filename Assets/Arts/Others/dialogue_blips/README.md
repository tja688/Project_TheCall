# Dialogue Blips

Voice blips and a typewriter dialogue box for Godot 4.3+: voices of five syllables each, a
`DialogueBox` node that types lines out and blips as it goes, a Theme per style, and a demo.

**The full pack:** **[Dialogue Blips](https://heyheythere.itch.io/dialogue-blips)** has 32 voices (chip, talking,
tuned and character voices, from robots and ghosts to monsters and fairies) and 8 styles (adding
wood, comic, terminal, cyan, gilded and horror). Same files and names: install it over this one.

## Use it

1. Copy `addons/dialogue_blips/` into your project.
2. Project Settings > *Rendering > Textures > Default Texture Filter*: **Nearest**, or the pixels blur.
3. Draw the UI at a whole-number scale. The art is 1x, made for a UI about 240 pixels high: use a
   small viewport (480x270) with *Stretch Mode* `canvas_items`, or set
   `get_tree().root.content_scale_factor` to 2, 3 or 4, as `demo/demo.gd` does.
4. Add a `DialogueBox`, size it (the art suits 58 pixels or more high), set its Theme to one of
   `themes/` and its `voice` to one of `voices/<name>/<name>.tres`, then:

```gdscript
$DialogueBox.say("Hello there! [wait=0.3]Nice day, isn't it?", "Ann", null, ann_face, ann_talking)
var picked := await $DialogueBox.play([
	{"text": "Halt!", "name": "Knight", "voice": knight_voice, "portrait": knight_face},
	{"text": "Pay the toll?", "name": "Knight", "choices": ["Pay", "Run"]},
])
```

## DialogueBox

A Control that builds its panel, name plate, portrait, text, next arrow and choice list itself.
`say()`, `ask()`, `play()` and `next()` drive it, and a click or `ui_accept` calls `next()` unless
`takes_input` is off. `say()` opens a hidden box, and `play()` closes it at the end.

A line is BBCode. Two commands go in it too: `[wait=0.5]` holds the typing that many seconds and
`[speed=2]` types at that many times `characters_per_second` from there to the line's end.

The speaker's voice is the one passed to `say()`, else `voices[speaker]`, else `voice`. Turn
`blips` off to mute the voice while `blipped` and the talking face go on, say to play your own
sounds from the signal. The open, close, next, move and select sounds are under *Sounds*; set one
to empty to silence it.

`panel`, `portrait`, `text` (a RichTextLabel), `name_plate`, `name_label`, `next_mark`, `choices`
and `choice_list` are the box's parts, there to restyle or move.

## BlipVoice

A Resource: `sounds`, the syllables, and how it speaks. `every` letters per blip; `pitch` and
`spread`, the semitones each letter wanders from it; `question_lift`, the semitones a question's
last word rises. `by_letter` ties each letter to one syllable and pitch, so a name always sounds
the same; off, they're picked at random. Make your own from any short sounds.

## Themes

`themes/<style>.tres` sets the font and these type variations, so a theme of your own needs the
same names:

| Type | Base | Styles |
|---|---|---|
| `DialogueBox` | PanelContainer | `panel`; icon `next`; constants `name_x`, `name_y` (the plate's offset), `separation`, `next_blink` (1 blinks the arrow, 0 bobs it) |
| `DialogueName` | PanelContainer | `panel`, the name plate |
| `DialogueNameLabel` | Label | `font_color` |
| `DialogueText` | RichTextLabel | `default_color`, shadow, `line_separation` |
| `DialogueChoice` | Button | `hover`, `focus` and `pressed` highlight the choice |

## Files

- `voices/<voice>/`: the `BlipVoice` and its five syllables as WAV (44.1 kHz, 16-bit).
- `sfx/`: the box's open, close, next, move and select sounds.
- `styles/<style>/`: the panel, name plate, choice highlight and next arrow as PNGs; the theme
  sets their 9-slice margins.
- `fonts/`: the pixel fonts, BMFont.
- `kit.json`: the voices by group with a line about each, and the styles.
- `demo/`: the demo scene and its art, the portraits (each with a `_talk` frame) and the town.
