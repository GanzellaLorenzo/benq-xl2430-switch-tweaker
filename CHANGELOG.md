# Changelog

The project was built by reverse-engineering a single monitor over one
afternoon, so the version numbers track the actual order of discoveries.

---

## v12

- **Leitura estável** (`ReadStable`): o cache DDC devolve valor velho, então uma
  leitura só não vale. Duas leituras concordando, 150 ms apartadas, valem.
- **Settle de 1.200 ms** depois da troca de modo (era 700). Abaixo disso o
  scaler ainda descarta writes.
- **Confirmação de boot cancelada** se você apertar um botão antes dela. Era ela
  que atropelava o primeiro perfil depois do logon.
- Medido: 12 de 12 trocas corretas em 3 rodadas. Custo: ~3,7 s por aperto.

## v11

- **Escritas verificadas.** O scaler descarta writes que chegam enquanto está
  aplicando a mudança anterior. Isso fazia o brilho não mudar entre 1/2/3 (o log
  dizia `Area=44` e a tela continuava em 48) e fazia o pulso do Instant Mode
  terminar em `0` — o sintoma idêntico a "Instant Mode off". `SetVerified` lê de
  volta e repete até confirmar; Area/Intensity usam ele.
- **Settle de 700 ms** depois da troca de modo (era 180).
- **Reparo no aperto**: se `0xD5` estiver em `0`, religa antes de aplicar.

## v9

- **Re-lock automático depois do wake.** O monitor entra em standby e volta com o
  scanout travado (imagem dupla, imune a desligar o Blur Reduction). Ao detectar
  que o DDC voltou, o keeper aplica o pulso do Instant Mode sem intervenção.
  Medido: 2,0 s do wake até a imagem correta.
- **Leitura antes de escrita** (`SetIfDiffers`). Leitura não pisca, escrita sim.
  Isso corta as piscadas de 5–6 para 2 por aperto, o tempo de 1,9 s para 1,6 s
  e os ciclos de NVRAM de 11 para 3.
- `needRelock` também arma no primeiro start, então o logon já corrige um
  estado herdado de um boot anterior.
- **Pulso lento, e só no wake.** Medido: o monitor aplica um write de VCP de
  forma assíncrona e o **scaler** leva ~2 s para processar o `0`. Com 700 ms o
  segundo pulso caía no vazio: o registrador ia para `1`, mas a imagem continuava
  dupla. Confirmado em hardware que **2500 ms** entre as bordas realmente
  destrava o scanout.

  Como a troca de modo já reaplica o timing, o pulso passou a rodar **somente**
  no wake. Resultado: aperto de botão em 0,90 s (era 1,9 s no original) e wake
  em 4,9 s.

- **Segundo relock 25 s depois, só no boot.** No boot o driver de vídeo ainda
  aplica o modo depois do logon, e isso trava o scaler de novo logo após o
  primeiro relock. Um segundo pulso 25 s depois cobre essa janela. Ele **não**
  roda depois de um wake: acordar o monitor e começar a jogar não pode levar um
  pulso no meio da partida.

- **Armar só depois de ~15 s de DDC caído** (`FAILS_TO_ARM = 21`, antes eram 5).
  Um jogo que troca de resolução ao abrir derruba o DDC por 2–3 s; com o
  limiar antigo isso disparava um relock à toa. Com 15 s, só standby de verdade
  arma. Durante o jogo o keeper fica só lendo — nunca escreve.

## v8

- Reassert **AMA High** (`0xF0 = 1`) and **Display Mode Full** (`0x86 = 2`) on
  every profile. Both are part of the state the presets clobber.
- AMA meaning confirmed by three independent sources (BL2420PT register dump,
  XL2735 thread, OorBuster source).

## v7

- Fix: pressing the **same** button twice did nothing. `lastMode` was latched
  before the restore and never reset, so a second press compared equal and was
  skipped — leaving the monitor stuck on the preset. Removed entirely; the loop
  now derives everything from `0xDC`.
- Stopwatch-based scheduling (the blocking DDC calls made `Thread.Sleep`
  drift).
- Pause file `%TEMP%\xlkeeper.pause`.
- Auto Mode polled every 5th cycle instead of every cycle.
- Stuck detection with back-off.

## v6

- Map each button to a user profile instead of a fixed Standard.
- **Discovery:** the presets do not only change colour — they also reset the
  strobe to `Area 10 / Intensity 10`. Restoring `0xDC` alone was not enough.

## v5

- **Discovery:** `0xD8` is the Auto Mode button (`4` off / `5` on), separate
  from the picture mode. Force it off.
- Pause file and loop hardening.

## v4

- **Discovery:** `0xF0` is AMA and `0x86` is Display Mode. The presets drop AMA
  and can leave the mode on Aspect, which shrinks the image once the scaler
  believes it is running 1440 lines.

## v3

- Add per-button Area/Intensity pairs.
- **Discovery:** writing `0xDC = 0` restores the strobe values stored with
  Standard automatically. The extra writes are only needed to override them
  with something else.

## v2

- **Discovery:** the double image that survives Blur Reduction OFF is Instant
  Mode stuck at the scaler. `0xD5 = 0 → 1` re-locks the scanout.
- This was the whole reason the project exists; v1 could not fix it.

## v1

- Poll `0xDC` every 700 ms and write `0` when it is not Standard.
- Worked for colour, not for the double image.
