class_name BlipVoice
extends Resource
## A speaking voice: short syllables a DialogueBox plays as its text types out.

## The syllables. One is picked per blip.
@export var sounds: Array[AudioStream] = []
## Letters per blip: 1 blips on every letter, 3 on every third.
@export_range(1, 4) var every := 2
## Semitones the pitch wanders either side of `pitch`.
@export_range(0.0, 12.0) var spread := 1.0
@export_range(0.25, 4.0) var pitch := 1.0
@export_range(-40.0, 12.0) var volume_db := 0.0
## Semitones a question's last word rises by.
@export_range(0.0, 12.0) var question_lift := 2.0
## Picks each blip's syllable and pitch from its letter, so a word always sounds the same.
## Off: at random.
@export var by_letter := true


## The syllable for `letter` and the pitch scale to play it at, `lift` semitones up.
func pick(letter: String, lift := 0.0) -> Array:
	var code := letter.to_lower().unicode_at(0)
	var i := code % sounds.size() if by_letter else randi() % sounds.size()
	var step := float((code * 7) % 5 - 2) / 2.0 if by_letter else randf_range(-1.0, 1.0)
	return [sounds[i], pitch * pow(2.0, (step * spread + lift) / 12.0)]
