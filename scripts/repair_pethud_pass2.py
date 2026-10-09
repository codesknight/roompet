"""Second pass: repairs damaged lines by fuzzy-matching them against the committed version.

The exact-match pass only worked when the surviving text lined up perfectly. A close-match pass
catches the rest of the lines that are simply older code, leaving only the lines that were
written after the last commit for manual repair.
"""

import difflib
import re
import subprocess
import sys

PATH = r"D:\projects\dsh-unity\UnityMCPProject\Assets\Pet\Scripts\UI\PetHud.cs"
REPO = r"D:\projects\dsh-unity"
HEAD_PATH = "UnityMCPProject/Assets/Pet/Scripts/UI/PetHud.cs"
HOLE = "\x00"

HEAD = subprocess.run(
    ["git", "-C", REPO, "show", "HEAD:" + HEAD_PATH],
    capture_output=True, check=True).stdout.decode("utf-8")
head_lines = [line for line in HEAD.split("\n")]
normalised = [line.replace(" ", "") for line in head_lines]

lines = open(PATH, encoding="utf-8").read().split("\n")

fixed = 0
todo = []
for index, line in enumerate(lines):
    if HOLE not in line:
        continue

    flat = line.replace(HOLE, "").replace(" ", "")
    best_ratio, best = 0.0, None
    for candidate, candidate_flat in zip(head_lines, normalised):
        if abs(len(candidate_flat) - len(flat)) > 12:
            continue
        ratio = difflib.SequenceMatcher(None, flat, candidate_flat).ratio()
        if ratio > best_ratio:
            best_ratio, best = ratio, candidate

    if best is not None and best_ratio >= 0.72:
        lines[index] = best
        fixed += 1
    else:
        todo.append((index + 1, line, best, best_ratio))

print("repaired by close match:", fixed)
print("still to rewrite:", len(todo))

open(r"D:\projects\dsh-unity\scripts\_manual.txt", "w", encoding="utf-8").write(
    "\n".join(f"--- line {number} (best {ratio:.2f})\nDAMAGED: {line}\n"
              f"  head: {best}"
              for number, line, best, ratio in todo))

open(PATH, "w", encoding="utf-8", newline="").write("\n".join(lines))
print("holes left:", "\n".join(lines).count(HOLE))
sys.exit(0)
