/** how long a line of `text` takes to come in at `step` ms a word */
export function revealTime(text: string, step = 45) {
  const noSpaces = /[\u3040-\u9fff\uff00-\uffef]/.test(text);
  const n = noSpaces ? Array.from(text).length : text.split(/\s+/).length;
  return n * step + 900;
}
