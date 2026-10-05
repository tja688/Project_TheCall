extends Control
## The kit in a little town at dusk: pick a voice to hear its speaker say a line, a style to dress
## the box, type a line of your own, or play a short scene with a choice in it.

const KIT := "res://addons/dialogue_blips/"
const ART_HEIGHT := 240.0
## Who speaks in a voice; the rest take the faces in turn.
const CAST := {"deep": "knight", "nasal": "witch", "robot": "robot", "ghost": "ghost", "tiny": "cat",
	"gruff": "merchant", "child": "kid", "alien": "slime"}
const FACES: Array[String] = ["kid", "cat", "witch", "merchant", "knight", "robot", "slime", "ghost"]
const LINES: Array[String] = [
	"Hello there! Lovely evening for an adventure, isn't it?",
	"The bridge is out. You'll have to find [wait=0.3]another way across...",
	"[wave]Ooh,[/wave] shiny! Is that for me?",
	"I've been waiting here [wait=0.4][speed=0.6]a very long time.",
	"Psst. Over here. Want to buy something?",
	"[shake]RUN![/shake] It's right behind you!",
	"One potion, two potions, three... [wait=0.3]four?",
	"Welcome to the shop! Everything is half price today.",
	"Do you hear that? [wait=0.5]Footsteps.",
	"Good night! Sleep well, and don't forget to save.",
]

## Store capture only: talks through the voices by itself, a new style each line.
@export var auto := false

var box: DialogueBox
var voice_name := ""
var style := ""
var entry: LineEdit
var talk_button: Button
var voice_buttons := {}
var style_buttons := {}
var _said := 0
var _wait := 0.0


## Draws at a whole-number scale so the art pixels stay square: the largest that still fits
## `art_height` pixels of art on screen.
static func pixel_scale(node: Node, art_height: float) -> void:
	var window := node.get_tree().root
	window.content_scale_factor = 1.0
	window.content_scale_factor = maxf(1.0, floorf(window.get_visible_rect().size.y / art_height))


static func kit() -> Dictionary:
	return JSON.parse_string(FileAccess.get_file_as_string(KIT + "kit.json"))


## The voices installed, in kit.json's order: the free sampler has six.
static func voices() -> Array[String]:
	var out: Array[String] = []
	for v: String in kit().voices:
		if ResourceLoader.exists(KIT + "voices/%s/%s.tres" % [v, v]):
			out.append(v)
	return out


static func styles() -> Array[String]:
	var out: Array[String] = []
	for s: String in kit().styles:
		if ResourceLoader.exists(KIT + "themes/%s.tres" % s):
			out.append(s)
	return out


## The voice if it's installed, else one that is.
static func voice(name: String) -> BlipVoice:
	var have := voices()
	if name not in have:
		name = have[name.hash() % have.size()]
	return load(KIT + "voices/%s/%s.tres" % [name, name])


static func style_theme(name: String) -> Theme:
	return load(KIT + "themes/%s.tres" % name)


static func face(who: String, talking := false) -> Texture2D:
	return load(KIT + "demo/art/%s%s.png" % [who, "_talk" if talking else ""])


static func cast(voice_name: String) -> String:
	return CAST.get(voice_name, FACES[kit().voices.keys().find(voice_name) % FACES.size()])


## A box at `at`, `width` wide, saying `line` as `speaker` in `voice_name`'s voice and face, typed
## out already.
static func shown_box(parent: Control, style_name: String, at: Vector2, width: float, height: float,
		speaker: String, voice_name: String, line: String) -> DialogueBox:
	var b := DialogueBox.new()
	b.theme = style_theme(style_name)
	b.position = at
	b.size = Vector2(width, height)
	b.open_sound = null
	parent.add_child(b)
	var who := cast(voice_name)
	b.say(line, speaker, null, face(who))
	b.next()
	return b


func _ready() -> void:
	get_viewport().canvas_item_default_texture_filter = Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST  # project.godot's, for a project without it
	pixel_scale(self, ART_HEIGHT)
	var view := get_viewport_rect().size
	var backdrop := TextureRect.new()
	backdrop.texture = load(KIT + "demo/art/backdrop.png")
	backdrop.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	backdrop.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	backdrop.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(backdrop)
	theme = style_theme(styles()[0])
	var left := roundf(view.x / 2 - 205)
	var voices_panel := _voice_panel()
	voices_panel.position = Vector2(left, 6)
	var styles_panel := _style_panel()
	styles_panel.position = Vector2(left + 410 - styles_panel.size.x, 6)
	_entry_row(Vector2(left, 6 + maxf(voices_panel.size.y, styles_panel.size.y) + 4), 410)
	box = DialogueBox.new()
	var foot := 30.0 if get_tree().root.has_node("FullPack") else 14.0  # clear of a sampler's line to its full pack
	box.position = Vector2(left, view.y - 58 - foot)
	box.size = Vector2(410, 58)
	add_child(box)
	box.blips = Engine.get_write_movie_path() == ""  # store captures are silent, and a sound playing as they quit leaks
	box.typed.connect(func() -> void: _wait = 1.0)
	set_style(styles()[0])
	voice_name = voices()[0]
	voice_buttons[voice_name].button_pressed = true
	box.open_sound = null
	box.say("Pick a voice to hear it speak, or type a line of your own. Click here to skip ahead.",
		"Dialogue Blips", voice(voice_name), face("kid"), face("kid", true))
	box.open_sound = preload("../sfx/open.wav")


func _panel() -> PanelContainer:
	var p := PanelContainer.new()
	p.theme_type_variation = &"DialogueBox"
	add_child(p)
	return p


func _button(text: String, group: ButtonGroup) -> Button:
	var b := Button.new()
	b.text = text
	b.theme_type_variation = &"DialogueChoice"
	b.alignment = HORIZONTAL_ALIGNMENT_LEFT
	b.toggle_mode = true
	b.button_group = group
	b.focus_mode = Control.FOCUS_NONE
	return b


func _heading(text: String) -> Label:
	var l := Label.new()
	l.text = text
	l.theme_type_variation = &"DialogueNameLabel"
	return l


func _voice_panel() -> PanelContainer:
	var p := _panel()
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 2)
	p.add_child(row)
	var info := kit()
	var group := ButtonGroup.new()
	var have := voices()
	for g: String in info.groups:
		var names: Array = have.filter(func(v: String) -> bool: return info.voices[v].group == g)
		if names.is_empty():
			continue
		var column := VBoxContainer.new()
		column.add_theme_constant_override("separation", 0)
		column.custom_minimum_size.x = 66
		row.add_child(column)
		column.add_child(_heading(info.groups[g]))
		for v: String in names:
			var b := _button(v.capitalize(), group)
			b.tooltip_text = info.voices[v].about
			b.pressed.connect(pick_voice.bind(v))
			column.add_child(b)
			voice_buttons[v] = b
	p.size = p.get_combined_minimum_size()
	return p


func _style_panel() -> PanelContainer:
	var p := _panel()
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 0)
	column.custom_minimum_size.x = 62
	p.add_child(column)
	column.add_child(_heading("Style"))
	var group := ButtonGroup.new()
	for s in styles():
		var b := _button(s.capitalize(), group)
		b.pressed.connect(set_style.bind(s))
		column.add_child(b)
		style_buttons[s] = b
	p.size = p.get_combined_minimum_size()
	return p


func _entry_row(at: Vector2, width: float) -> void:
	var row := HBoxContainer.new()
	row.position = at
	row.size = Vector2(width, 0)
	row.add_theme_constant_override("separation", 4)
	add_child(row)
	entry = LineEdit.new()
	entry.placeholder_text = "Type a line, then Enter"
	entry.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	entry.text_submitted.connect(func(line: String) -> void:
		if line.strip_edges() != "":
			_speak(voice_name, line)
		entry.release_focus())
	row.add_child(entry)
	talk_button = Button.new()
	talk_button.text = "Play a scene"
	talk_button.focus_mode = Control.FOCUS_NONE
	talk_button.pressed.connect(scene)
	row.add_child(talk_button)


func set_style(name: String) -> void:
	style = name
	theme = style_theme(name)
	style_buttons[name].button_pressed = true
	# the demo's own field and button in the box's name plate
	var plate := load(KIT + "styles/%s/name.png" % name) as Texture2D
	for c: Control in [entry, talk_button]:
		for state in ["normal", "focus", "hover", "pressed", "read_only"]:
			var sb := StyleBoxTexture.new()
			sb.texture = plate
			sb.set_texture_margin_all(5)
			sb.set_content_margin_all(4)
			sb.content_margin_top = 3
			sb.content_margin_bottom = 3
			sb.modulate_color = Color(1.25, 1.25, 1.25) if state in ["hover", "focus"] else Color.WHITE
			c.add_theme_stylebox_override(state, sb)
		var ink := theme.get_color("font_color", "DialogueNameLabel")
		for key in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "caret_color"]:
			c.add_theme_color_override(key, ink)
		c.add_theme_color_override("font_placeholder_color", Color(ink, 0.6))


func pick_voice(name: String) -> void:
	voice_name = name
	_speak(name, LINES[kit().voices.keys().find(name) % LINES.size()])


func _speak(name: String, line: String) -> void:
	var who := cast(name)
	box.say(line, name.capitalize(), voice(name), face(who), face(who, true))


## Two speakers and a choice, played through DialogueBox.play.
func scene() -> void:
	talk_button.disabled = true
	var knight := {"name": "Knight", "voice": voice("deep"), "portrait": face("knight"), "talking": face("knight", true)}
	var kid := {"name": "Kid", "voice": voice("child"), "portrait": face("kid"), "talking": face("kid", true)}
	var choice := await box.play([
		knight.merged({"text": "Halt! None shall cross this bridge [wait=0.3]without paying the toll."}),
		kid.merged({"text": "Um... How much is the toll?"}),
		knight.merged({"text": "Three gold. Or one [wave]really[/wave] good joke.", "choices": ["Pay three gold", "Tell a joke"]}),
	])
	await get_tree().create_timer(0.3).timeout
	var reply := "Thank you kindly. Mind the gap!" if choice == 0 else "[shake]Ha![/shake] Ha ha! [wait=0.3]Fine. Off you go."
	box.say(reply, "Knight", knight.voice, knight.portrait, knight.talking)
	talk_button.disabled = false


func _process(delta: float) -> void:
	if not auto or box.is_typing():
		return
	_wait -= delta
	if _wait > 0.0:
		return
	_said += 1
	var have := voices()
	var order: Array[String] = ["deep", "robot", "child", "ghost", "tiny", "nasal", "gruff", "alien"]
	var v: String = order[_said % order.size()] if order[_said % order.size()] in have else have[_said % have.size()]
	var all := styles()
	set_style(all[(all.find(style) + 1) % all.size()])
	voice_buttons[v].button_pressed = true
	pick_voice(v)
	_wait = 99.0
