# Codex usage widget — throwaway UI prototype

Question: which visual structure makes weekly Codex usage easiest to read without becoming distracting?

Open `index.html` directly in a browser. Switch between variants with the on-screen arrows, the keyboard arrow keys, or `?variant=A`, `?variant=B`, and `?variant=C` in the address.

Variant A is the selected direction. Its purple progress bar is draggable so different remaining-usage values can be previewed; when the bar has keyboard focus, the arrow keys adjust it one percentage point at a time.

## Approved direction

Variant A (`Quiet card`) was approved with a 143 px outer width, 16 px corner radius, a compact 10 px percentage label, and a slim inset purple progress bar. The production widget should preserve that visual direction while replacing the prototype's fixed sample data and drag control with live Codex usage data.

This is disposable prototype code, not the production Windows application. It uses fixed sample data and makes no account requests.
