# In-app help

The editor's Help window and its (?) popups are built from these Markdown files, which are embedded in the app.

- `*.md` (this folder): one Help window topic each. The first line is `# Title`.
- `tips/*.md`: the short text of one (?) popup. The first line is `# Title`, the second
  `<!-- topic: <topic file name> -->` names the topic its "More in the Help window" link opens.

The renderer understands `## headings`, `- ` bullets (indent with two spaces to continue a bullet), `**bold**`
and `` `code` ``. This README is not shown in the app.
