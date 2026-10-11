// Regenerates UnityMCPProject/Assets/Pet/Scripts/Core/EnglishCorner.Data.cs from the
// KyleBing/english-vocabulary full_line_jsonl sources, tagging each word with its level
// (初中 / 高中 / 大学) so the English corner can offer separate dictionaries.
//
// Usage (from the repo root):
//   node scripts/gen_english_words.js
//
// Sources (UTF-8, one JSON object per line):
//   Builds/chuzhong.jsonl   -> Category 初中
//   Builds/gaozhong.jsonl   -> Category 高中
//   Builds/cet4.jsonl       -> Category 大学 (四级)
//   Builds/cet6.jsonl       -> Category 大学 (六级)
//
// Rules (same as round 55):
//   - word must be pure ASCII letters ([a-zA-Z]+)
//   - drop translations whose text mentions 人名 / 地名 (they are proper-noun glosses)
//   - skip the word entirely if no translation survives
//   - Meaning = up to 3 surviving translations, each "type. text", joined by a space
//   - Example / ExampleTranslation = first sentence pair, or a fallback sentence
//   - dedupe by lowercase word; first category wins (初中 > 高中 > 大学)
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');

const sources = [
  { file: 'chuzhong.jsonl', category: '初中' },
  { file: 'gaozhong.jsonl', category: '高中' },
  { file: 'cet4.jsonl', category: '大学' },
  { file: 'cet6.jsonl', category: '大学' },
];

function parseLine(line) {
  line = line.trim();
  if (!line) return null;
  try { return JSON.parse(line); } catch (e) { return null; }
}

function glossSurvives(t) {
  const text = (t.translation || '');
  return !(text.includes('人名') || text.includes('地名'));
}

function buildMeaning(entry) {
  const kept = [];
  const list = Array.isArray(entry.translations) ? entry.translations : [];
  for (const t of list) {
    if (!glossSurvives(t)) continue;
    if (kept.length >= 3) break;
    const text = (t.translation || '').trim();
    if (!text) continue;
    const type = (t.type || '').trim();
    kept.push(type ? type + '. ' + text : text);
  }
  return kept.length ? kept.join(' ') : null;
}

function exampleFor(entry, word, meaning) {
  const sentences = Array.isArray(entry.sentences) ? entry.sentences : [];
  if (sentences.length && sentences[0].sentence && sentences[0].sentence.trim()) {
    return {
      example: sentences[0].sentence.trim(),
      translation: (sentences[0].translation || '').trim() || meaning,
    };
  }
  return {
    example: 'I use the word "' + word + '" in a sentence.',
    translation: meaning,
  };
}

function csharpString(s) {
  return s
    .replace(/\\/g, '\\\\')
    .replace(/"/g, '\\"')
    .replace(/\r/g, '')
    .replace(/\n/g, '\\n')
    .replace(/\t/g, '\\t');
}

function main() {
  const byWord = new Map(); // lowercase -> entry

  for (const src of sources) {
    const p = path.join(root, 'Builds', src.file);
    if (!fs.existsSync(p)) {
      console.error('missing source: ' + p);
      process.exit(1);
    }
    const text = fs.readFileSync(p, 'utf8');
    const lines = text.split(/\r?\n/);
    let kept = 0;
    for (const line of lines) {
      const entry = parseLine(line);
      if (!entry) continue;
      const word = (entry.word || '').trim();
      if (!/^[a-zA-Z]+$/.test(word)) continue;

      const meaning = buildMeaning(entry);
      if (!meaning) continue;

      const key = word.toLowerCase();
      if (byWord.has(key)) continue; // first category wins

      const ex = exampleFor(entry, word, meaning);
      byWord.set(key, {
        word,
        meaning,
        example: ex.example,
        translation: ex.translation,
        category: src.category,
      });
      kept++;
    }
    console.log(src.file + ' -> ' + src.category + ' (' + kept + ' new)');
  }

  const words = Array.from(byWord.values());
  words.sort((a, b) => a.word.toLowerCase().localeCompare(b.word.toLowerCase()));

  const lines = [];
  lines.push('using System.Collections.Generic;');
  lines.push('');
  lines.push('namespace DshPet');
  lines.push('{');
  lines.push('    /// <summary>英语角词库数据文件（partial）。初中 / 高中 / 大学 分词典，含词性、释义、例句与中文译文。数据来源：KyleBing/english-vocabulary（公开学习资源）。</summary>');
  lines.push('    public static partial class EnglishCorner');
  lines.push('    {');
  lines.push('        public static readonly EnglishWord[] Words =');
  lines.push('        {');
  for (const w of words) {
    lines.push(
      '            new EnglishWord { Word = "' + csharpString(w.word) +
      '", Meaning = "' + csharpString(w.meaning) +
      '", Example = "' + csharpString(w.example) +
      '", ExampleTranslation = "' + csharpString(w.translation) +
      '", Category = "' + w.category + '" },'
    );
  }
  lines.push('        };');
  lines.push('    }');
  lines.push('}');

  let body = lines.join('\n') + '\n';
  // UTF-8 with BOM so the Chinese literals compile correctly on a Chinese-locale editor.
  const out = path.join(root, 'UnityMCPProject', 'Assets', 'Pet', 'Scripts', 'Core', 'EnglishCorner.Data.cs');
  fs.writeFileSync(out, '\uFEFF' + body, 'utf8');

  const counts = { '初中': 0, '高中': 0, '大学': 0 };
  for (const w of words) counts[w.category]++;
  console.log('total words: ' + words.length + ' (初中 ' + counts['初中'] + ' / 高中 ' + counts['高中'] + ' / 大学 ' + counts['大学'] + ')');
  console.log('wrote ' + out + ' (' + Math.round(fs.statSync(out).size / 1024) + ' KB)');
}

main();
