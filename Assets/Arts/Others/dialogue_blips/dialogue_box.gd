class_name DialogueBox
extends Control
## A dialogue box that types its lines out letter by letter while the speaker's BlipVoice blips.
## Size it like any Control and give it a Theme from themes/. A line is BBCode, plus
## [wait=0.5] (a pause, in seconds) and [speed=2] (typing speed, times the usual, to the line's end).

signal blipped(letter: String)
## The whole line is showing.
signal typed
## The player moved on from a typed line.
signal advanced
signal chosen(index: int)

const COMMANDS := "\\[(wait|speed)=([0-9.]+)\\]"

## Who talks when `say` names no voice and `voices` has none for the speaker.
@export var voice: BlipVoice
## Voices by speaker name.
@export var voices: Dictionary = {}
@export_range(1.0, 200.0) var characters_per_second := 40.0
## Seconds held after a comma, semicolon or colon.
@export_range(0.0, 2.0) var comma_pause := 0.12
## Seconds held after a full stop, ! or ?.
@export_range(0.0, 2.0) var stop_pause := 0.3
## Off, the voice is silent; `blipped` and the talking face go on.
@export var blips := true
## A click on the box or ui_accept finishes the line, or moves on.
@export var takes_input := true
@export var bus := &"Master"
@export_group("Sounds")
@export var open_sound: AudioStream = preload("sfx/open.wav")
@export var close_sound: AudioStream = preload("sfx/close.wav")
@export var next_sound: AudioStream = preload("sfx/next.wav")
@export var move_sound: AudioStream = preload("sfx/move.wav")
@export var select_sound: AudioStream = preload("sfx/select.wav")
@export_range(-40.0, 12.0) var sounds_db := -4.0

var panel: PanelContainer
var portrait: TextureRect
var text: RichTextLabel
var name_plate: PanelContainer
var name_label: Label
var next_mark: TextureRect
## The choices' panel, over the box's right end.
var choices: PanelContainer
var choice_list: VBoxContainer

var _row: HBoxContainer
var _players: Array[AudioStreamPlayer] = []
var _ui_player: AudioStreamPlayer
var _next_player := 0
var _commands_re := RegEx.create_from_string(COMMANDS)
var _tags_re := RegEx.create_from_string("\\[[^\\]]*\\]")
var _plain := ""
var _commands := {}  # visible character index: [[command, value], ...] to run before it shows
var _lifts := PackedFloat32Array()  # 1 on a question's last word
var _speaking: BlipVoice
var _typing := false
var _clock := 0.0
var _speed := 1.0
var _letters := 0
var _options: Array = []
var _rest_face: Texture2D
var _talking_face: Texture2D
var _mouth := 0.0
var _time := 0.0


func _init() -> void:
	panel = PanelContainer.new()
	panel.theme_type_variation = &"DialogueBox"
	panel.set_anchors_preset(Control.PRESET_FULL_RECT)
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(panel)
	_row = HBoxContainer.new()
	_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_child(_row)
	portrait = TextureRect.new()
	portrait.stretch_mode = TextureRect.STRETCH_KEEP_CENTERED
	portrait.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
	portrait.hide()
	_row.add_child(portrait)
	text = RichTextLabel.new()
	text.bbcode_enabled = true
	text.scroll_active = false
	text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	text.visible_characters_behavior = TextServer.VC_CHARS_AFTER_SHAPING
	text.theme_type_variation = &"DialogueText"
	text.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	text.size_flags_vertical = Control.SIZE_EXPAND_FILL
	text.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_row.add_child(text)
	name_plate = PanelContainer.new()
	name_plate.theme_type_variation = &"DialogueName"
	name_plate.mouse_filter = Control.MOUSE_FILTER_IGNORE
	name_plate.hide()
	add_child(name_plate)
	name_label = Label.new()
	name_label.theme_type_variation = &"DialogueNameLabel"
	name_plate.add_child(name_label)
	next_mark = TextureRect.new()
	next_mark.mouse_filter = Control.MOUSE_FILTER_IGNORE
	next_mark.hide()
	add_child(next_mark)
	choices = PanelContainer.new()
	choices.theme_type_variation = &"DialogueBox"
	choices.hide()
	add_child(choices)
	choice_list = VBoxContainer.new()
	choice_list.add_theme_constant_override("separation", 0)
	choices.add_child(choice_list)
	for i in 4:  # blips overlap at speed
		var p := AudioStreamPlayer.new()
		add_child(p)
		_players.append(p)
	_ui_player = AudioStreamPlayer.new()
	add_child(_ui_player)


func _notification(what: int) -> void:
	if what == NOTIFICATION_THEME_CHANGED or what == NOTIFICATION_RESIZED:
		_place.call_deferred()


func _process(delta: float) -> void:
	tick(delta)


## Types `line` out as `speaker`, in `speaking` or else the speaker's voice in `voices` or else
## `voice`, beside `face`; `talking`, if given, is the face shown on each blip.
func say(line: String, speaker := "", speaking: BlipVoice = null, face: Texture2D = null, talking: Texture2D = null) -> void:
	_clear_choices()
	_commands.clear()
	text.text = _strip_commands(line)
	text.visible_characters = 0
	_plain = text.get_parsed_text()
	_mark_questions()
	_speaking = speaking if speaking else voices.get(speaker, voice)
	name_label.text = speaker
	name_plate.visible = speaker != ""
	portrait.texture = face
	portrait.visible = face != null
	_rest_face = face
	_talking_face = talking
	_typing = true
	_clock = 0.0
	_speed = 1.0
	_letters = 0
	next_mark.hide()
	_place()
	if not visible:
		open()


## Like `say`, then offers `options` (Strings) and emits `chosen` with the one picked.
func ask(line: String, options: Array, speaker := "", speaking: BlipVoice = null, face: Texture2D = null, talking: Texture2D = null) -> void:
	say(line, speaker, speaking, face, talking)
	_options = options.duplicate()


## Plays a conversation, then closes the box. Each line is a Dictionary of `text` and any of
## `name`, `voice`, `portrait`, `talking` and `choices` (an Array of Strings). Returns the last
## choice made, or -1.
func play(lines: Array) -> int:
	var choice := -1
	for line: Dictionary in lines:
		if line.has("choices"):
			ask(line.text, line.choices, line.get("name", ""), line.get("voice"), line.get("portrait"), line.get("talking"))
			choice = await chosen
		else:
			say(line.text, line.get("name", ""), line.get("voice"), line.get("portrait"), line.get("talking"))
			await advanced
	close()
	return choice


## Shows the whole line if it's typing, else moves on.
func next() -> void:
	if _typing:
		_commands.clear()
		_finish()
	elif _options.is_empty() and visible and not _plain.is_empty():
		_sound(next_sound)
		next_mark.hide()
		advanced.emit()


func is_typing() -> bool:
	return _typing


func open() -> void:
	show()
	_sound(open_sound)
	if not is_inside_tree():
		return
	pivot_offset = size / 2
	scale = Vector2(1, 0.3)
	modulate.a = 0.0
	var tw := create_tween().set_parallel()
	tw.tween_property(self, "scale", Vector2.ONE, 0.12)
	tw.tween_property(self, "modulate:a", 1.0, 0.12)


func close() -> void:
	_typing = false
	_clear_choices()
	_sound(close_sound)
	if not is_inside_tree():
		hide()
		return
	pivot_offset = size / 2
	var tw := create_tween().set_parallel()
	tw.tween_property(self, "scale:y", 0.3, 0.1)
	tw.tween_property(self, "modulate:a", 0.0, 0.1)
	tw.chain().tween_callback(func() -> void:
		hide()
		scale = Vector2.ONE
		modulate.a = 1.0)


## Advances the typing by `delta` seconds; the box calls it every frame.
func tick(delta: float) -> void:
	_time += delta
	if _mouth > 0.0:
		_mouth -= delta
		if _mouth <= 0.0:
			portrait.texture = _rest_face
	if next_mark.visible:
		if get_theme_constant("next_blink", "DialogueBox"):
			next_mark.self_modulate.a = 1.0 if fmod(_time, 1.0) < 0.5 else 0.0
		else:
			next_mark.position.y = _next_y() + (1.0 if fmod(_time, 0.8) < 0.4 else 0.0)
	if not _typing:
		return
	_clock -= delta
	while _typing and _clock <= 0.0:
		_type_one()


func _type_one() -> void:
	var i := text.visible_characters
	if _commands.has(i):
		for command: Array in _commands[i]:
			if command[0] == "wait":
				_clock += command[1]
			else:
				_speed = maxf(command[1], 0.01)
		_commands.erase(i)
		if _clock > 0.0:
			return
	if i >= _plain.length():
		_finish()
		return
	text.visible_characters = i + 1
	var c := _plain[i]
	if _is_letter(c):
		_blip(c, i)
	var after := _plain[i + 1] if i + 1 < _plain.length() else " "
	var hold := 0.0
	if c in ".!?" and (after in " \n.!?\"')" or i + 1 == _plain.length()):
		hold = stop_pause
	elif c in ",;:" and after in " \n":
		hold = comma_pause
	_clock += (1.0 / characters_per_second + hold) / _speed


func _blip(c: String, i: int) -> void:
	_letters += 1
	var v := _speaking
	if v == null or v.sounds.is_empty() or (_letters - 1) % v.every != 0:
		return
	if blips:
		var pick := v.pick(c, _lifts[i] * v.question_lift)
		var p := _players[_next_player]
		_next_player = (_next_player + 1) % _players.size()
		p.stream = pick[0]
		p.pitch_scale = pick[1]
		p.volume_db = v.volume_db
		p.bus = bus
		p.play()
	if _talking_face:
		portrait.texture = _talking_face
		_mouth = 0.5 * v.every / (characters_per_second * _speed)
	blipped.emit(c)


func _finish() -> void:
	_typing = false
	text.visible_characters = -1
	portrait.texture = _rest_face
	typed.emit()
	if _options.is_empty():
		next_mark.show()
		_place()
	else:
		_show_choices()


func _show_choices() -> void:
	for i in _options.size():
		var b := Button.new()
		b.text = str(_options[i])
		b.theme_type_variation = &"DialogueChoice"
		b.alignment = HORIZONTAL_ALIGNMENT_LEFT
		b.pressed.connect(_choose.bind(i))
		b.mouse_entered.connect(b.grab_focus)
		choice_list.add_child(b)
	choices.show()
	_place()
	if is_inside_tree():
		(choice_list.get_child(0) as Button).grab_focus()
	for b in choice_list.get_children():
		b.focus_entered.connect(_sound.bind(move_sound))


func _choose(i: int) -> void:
	_sound(select_sound)
	_clear_choices()
	chosen.emit(i)


func _clear_choices() -> void:
	_options = []
	for b in choice_list.get_children():
		choice_list.remove_child(b)
		b.queue_free()
	choices.hide()


func _place() -> void:
	_row.add_theme_constant_override("separation", get_theme_constant("separation", "DialogueBox"))
	name_plate.size = name_plate.get_combined_minimum_size()
	name_plate.position = Vector2(get_theme_constant("name_x", "DialogueBox"), -get_theme_constant("name_y", "DialogueBox"))
	next_mark.texture = get_theme_icon("next", "DialogueBox")
	if next_mark.texture:
		next_mark.size = next_mark.texture.get_size()
		next_mark.position = Vector2(size.x - _inset(SIDE_RIGHT) - next_mark.size.x + 2, _next_y())
	choices.size = choices.get_combined_minimum_size()
	choices.position = Vector2(size.x - choices.size.x, -choices.size.y - 2)


func _next_y() -> float:
	return size.y - _inset(SIDE_BOTTOM) - (next_mark.size.y if next_mark.texture else 0.0) + 1


func _inset(side: Side) -> float:
	var sb := panel.get_theme_stylebox("panel")
	return sb.get_margin(side) if sb else 0.0


func _sound(stream: AudioStream) -> void:
	if stream and is_inside_tree():
		_ui_player.stream = stream
		_ui_player.volume_db = sounds_db
		_ui_player.bus = bus
		_ui_player.play()


func _gui_input(event: InputEvent) -> void:
	if takes_input and event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		next()
		accept_event()


func _unhandled_input(event: InputEvent) -> void:
	if takes_input and is_visible_in_tree() and event.is_action_pressed("ui_accept"):
		next()
		get_viewport().set_input_as_handled()


## The line without its [wait] and [speed], which go into `_commands` at the visible character
## they come before.
func _strip_commands(line: String) -> String:
	var out := ""
	var at := 0
	for m in _commands_re.search_all(line):
		out += line.substr(at, m.get_start() - at)
		var index := _visible_length(out)
		if not _commands.has(index):
			_commands[index] = []
		_commands[index].append([m.get_string(1), m.get_string(2).to_float()])
		at = m.get_end()
	return out + line.substr(at)


func _visible_length(bbcode: String) -> int:
	return _tags_re.sub(bbcode.replace("[lb]", "x").replace("[rb]", "x"), "", true).length()


func _mark_questions() -> void:
	_lifts.resize(_plain.length())
	_lifts.fill(0.0)
	var lifting := false
	var in_word := false
	for i in range(_plain.length() - 1, -1, -1):
		var c := _plain[i]
		if c == "?":
			lifting = true
			in_word = false
		elif lifting:
			if _is_letter(c):
				_lifts[i] = 1.0
				in_word = true
			elif in_word:
				lifting = false


static func _is_letter(c: String) -> bool:
	return c.to_upper() != c.to_lower() or (c >= "0" and c <= "9") or c.unicode_at(0) >= 0x3000
