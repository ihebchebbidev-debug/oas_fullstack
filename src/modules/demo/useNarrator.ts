/**
 * Browser narration for the guided demo.
 *
 * Uses the Web Speech API (`speechSynthesis`) so the tour speaks with no
 * backend and no API key. Two things make it sound human rather than robotic:
 *
 *  1. Voice scoring — cloud / "Natural" / "Neural" / "Google" voices are
 *     picked first (they are the modern neural ones), then known female
 *     names, and explicitly-male voices are pushed to the bottom.
 *  2. Prosody — the text is spoken sentence by sentence with a short breath
 *     between them, at a slightly slower rate and near-neutral pitch. This
 *     also avoids the Chrome bug that truncates long single utterances.
 */

import { useCallback, useEffect, useRef, useState } from 'react';

type Lang = 'fr' | 'en' | 'ar';

const BCP47: Record<Lang, string> = { fr: 'fr-FR', en: 'en-GB', ar: 'ar-TN' };

/** Modern neural / cloud engines — these are the ones that sound human. */
const PREMIUM_HINTS = [
  'natural', 'neural', 'premium', 'enhanced', 'siri', 'wavenet', 'studio', 'journey', 'online',
  'google', 'microsoft',
];

/** Voices that are female on macOS / iOS / Windows / Android / Chrome OS. */
const FEMALE_NAMES = [
  'amélie', 'amelie', 'audrey', 'aurelie', 'aurélie', 'marie', 'julie', 'céline', 'celine',
  'virginie', 'charlotte', 'chantal', 'denise', 'eloise', 'éloise', 'vivienne', 'brigitte',
  'samantha', 'victoria', 'karen', 'moira', 'tessa', 'fiona', 'serena', 'zira', 'susan',
  'catherine', 'sonia', 'libby', 'emma', 'ava', 'joanna', 'aria', 'jenny', 'michelle', 'nova',
  'salome', 'hala', 'laila', 'amira', 'zariyah', 'female', 'woman', 'femme',
];

/** Never pick these when a female voice exists. */
const MALE_NAMES = [
  'thomas', 'nicolas', 'henri', 'paul', 'daniel', 'alex', 'fred', 'tom', 'george', 'guy',
  'ryan', 'brian', 'david', 'mark', 'james', 'liam', 'oliver', 'male', 'homme', 'rémi', 'remi',
  'hamed', 'omar', 'shakir',
];

const hay = (v: SpeechSynthesisVoice) => `${v.name} ${v.voiceURI}`.toLowerCase();
const has = (v: SpeechSynthesisVoice, list: string[]) => list.some((w) => hay(v).includes(w));

/** Higher is better. */
function score(v: SpeechSynthesisVoice, lang: Lang): number {
  const tag = BCP47[lang].toLowerCase();
  const vlang = v.lang.toLowerCase().replace('_', '-');
  let s = 0;
  if (vlang === tag) s += 40;
  else if (vlang.startsWith(tag.slice(0, 2))) s += 25;
  if (has(v, FEMALE_NAMES)) s += 30;
  if (has(v, MALE_NAMES)) s -= 35;
  if (has(v, PREMIUM_HINTS)) s += 20;
  // Cloud voices (localService === false) are the neural ones on Chrome/Edge.
  if (!v.localService) s += 15;
  if (v.default) s += 2;
  return s;
}

function pickVoice(voices: SpeechSynthesisVoice[], lang: Lang): SpeechSynthesisVoice | null {
  if (!voices.length) return null;
  const base = BCP47[lang].slice(0, 2).toLowerCase();
  const local = voices.filter((v) => v.lang.toLowerCase().replace('_', '-').startsWith(base));
  const pool = local.length ? local : voices;
  return [...pool].sort((a, b) => score(b, lang) - score(a, lang))[0] ?? null;
}

/** Split into speakable sentences — short utterances sound better and never get cut. */
function sentences(text: string): string[] {
  return (text.match(/[^.!?…؟]+[.!?…؟]*/g) ?? [text])
    .map((s) => s.trim())
    .filter(Boolean);
}

export interface Narrator {
  supported: boolean;
  /** Speak a paragraph; resolves when it ends (or immediately if unsupported). */
  speak: (text: string, lang: Lang) => Promise<void>;
  cancel: () => void;
  pause: () => void;
  resume: () => void;
  /** Name of the voice currently selected, for the UI. */
  voiceName: string | null;
  muted: boolean;
  setMuted: (m: boolean) => void;
}

const wait = (ms: number) => new Promise<void>((r) => window.setTimeout(r, ms));

export function useNarrator(lang: Lang): Narrator {
  const supported = typeof window !== 'undefined' && 'speechSynthesis' in window;
  const [voices, setVoices] = useState<SpeechSynthesisVoice[]>([]);
  const [muted, setMuted] = useState(false);
  const runIdRef = useRef(0);

  useEffect(() => {
    if (!supported) return;
    const load = () => setVoices(window.speechSynthesis.getVoices());
    load();
    // Chrome populates the list asynchronously — poll briefly as a fallback.
    const timers = [200, 600, 1500].map((d) => window.setTimeout(load, d));
    window.speechSynthesis.addEventListener('voiceschanged', load);
    return () => {
      timers.forEach(window.clearTimeout);
      window.speechSynthesis.removeEventListener('voiceschanged', load);
    };
  }, [supported]);

  // Never leave a sentence hanging when the page unmounts.
  useEffect(() => () => { if (supported) window.speechSynthesis.cancel(); }, [supported]);

  const voice = supported ? pickVoice(voices, lang) : null;

  const speakOne = useCallback(
    (text: string, l: Lang, v: SpeechSynthesisVoice | null) =>
      new Promise<void>((resolve) => {
        const u = new SpeechSynthesisUtterance(text);
        u.lang = BCP47[l];
        if (v) u.voice = v;
        // Calm, human pacing: a touch slower than default, natural pitch.
        u.rate = l === 'ar' ? 0.92 : 0.95;
        u.pitch = 1.04;
        u.volume = 1;
        let done = false;
        const finish = () => { if (!done) { done = true; resolve(); } };
        u.onend = finish;
        u.onerror = finish;
        window.speechSynthesis.speak(u);
      }),
    [],
  );

  const speak = useCallback(
    async (text: string, l: Lang) => {
      if (!supported || muted) return;
      const synth = window.speechSynthesis;
      synth.cancel();
      if (synth.paused) synth.resume();
      const runId = ++runIdRef.current;
      const v = pickVoice(synth.getVoices(), l);
      const parts = sentences(text);
      for (let i = 0; i < parts.length; i++) {
        if (runId !== runIdRef.current) return;
        await speakOne(parts[i], l, v);
        if (runId !== runIdRef.current) return;
        // A short breath between sentences — the biggest anti-robot win.
        if (i < parts.length - 1) await wait(180);
      }
    },
    [supported, muted, speakOne],
  );

  const cancel = useCallback(() => {
    runIdRef.current++;
    if (supported) window.speechSynthesis.cancel();
  }, [supported]);
  const pause = useCallback(() => { if (supported) window.speechSynthesis.pause(); }, [supported]);
  const resume = useCallback(() => { if (supported) window.speechSynthesis.resume(); }, [supported]);

  return { supported, speak, cancel, pause, resume, voiceName: voice?.name ?? null, muted, setMuted };
}
