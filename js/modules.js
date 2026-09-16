(function (global) {
  'use strict';

  var ALLINONE = global.ALLINONE;

  ALLINONE.modules.register({
    id: 'general',
    name: 'General assistant',
    description: 'Core chat, greetings, small talk, time, date, basic math.',
    init: function () {}
  });

  ALLINONE.modules.register({
    id: 'math',
    name: 'Math',
    description: 'Percentages, square roots, and simple math questions.',
    init: function (app) {
      app.brain.registerIntent({
        id: 'math.percent',
        priority: 77,
        test: function (c) { return /\b\d+(?:\.\d+)?\s*%\s*of\s*\d+(?:\.\d+)?\b/.test(c.text); },
        respond: function (c) {
          var m = c.text.match(/(\d+(?:\.\d+)?)\s*%\s*of\s*(\d+(?:\.\d+)?)/);
          if (!m) return { text: 'Try "what is 15% of 80".' };
          var result = parseFloat(m[1]) / 100 * parseFloat(m[2]);
          return { text: m[1] + '% of ' + m[2] + ' = **' + app.brain.formatNumber(result) + '**' };
        }
      });
      app.brain.registerIntent({
        id: 'math.sqrt',
        priority: 77,
        test: function (c) { return /\b(square root|sqrt)\s*(?:of\s*)?\d+(?:\.\d+)?\b/.test(c.text); },
        respond: function (c) {
          var m = c.text.match(/(?:square root|sqrt)\s*(?:of\s*)?(\d+(?:\.\d+)?)/);
          if (!m) return { text: 'Try "square root of 144".' };
          var n = parseFloat(m[1]);
          return { text: '√' + n + ' = **' + app.brain.formatNumber(Math.sqrt(n)) + '**' };
        }
      });
    }
  });

  ALLINONE.modules.register({
    id: 'coding',
    name: 'Coding help',
    description: 'Programming concepts, HTML, CSS, JavaScript, debugging.',
    init: function (app) {
      app.brain.registerIntent({
        id: 'coding.main', priority: 62,
        test: function (c) { return /\b(html|css|javascript|js|python|java|c\+\+|programming|coding|code|developer|debug|debugging|variable|function|loop|array|object|bug|error|syntax|api|git|github|terminal)\b/.test(c.text) && !/\broblox\b/.test(c.text) && !/\b(game\s?dev|game development|gamedev|unity|godot)\b/.test(c.text); },
        respond: function (c) {
          if (/\bhtml\b/.test(c.text)) return { text: 'HTML is the structure of a webpage. Start with headings, paragraphs, links, images, buttons, forms, and meaningful classes/ids. What are you trying to build?' };
          if (/\bcss\b/.test(c.text)) return { text: 'CSS controls how a page looks and lays out. Flexbox, responsive media queries, and CSS variables are great fundamentals. What are you styling?' };
          if (/\b(javascript|js)\b/.test(c.text)) return { text: 'JavaScript adds behavior to webpages. Common fundamentals are variables, functions, arrays, objects, events, and async code. What are you trying to make?' };
          if (/\b(debug|debugging|bug|error)\b/.test(c.text)) return { text: 'Debugging tips:\n1. Read the error carefully.\n2. Change one thing at a time.\n3. Log suspicious values.\n4. Check names, operators, returns, and indexes.\n5. Narrow down where the problem starts.\n\nWhat error are you getting?' };
          return { text: 'I can help with HTML, CSS, JavaScript, programming concepts, debugging, project structure, and Git/GitHub basics. What are you building?' };
        }
      });
    }
  });

  ALLINONE.modules.register({
    id: 'gamedev',
    name: 'Game development',
    description: 'Game ideas, mechanics, project organization, general game dev.',
    init: function (app) {
      app.brain.registerIntent({
        id: 'gamedev.main', priority: 61,
        test: function (c) { return /\b(game\s?dev|game development|gamedev|unity|godot|game design|level design|game mechanic|game mechanics|make a game|build a game)\b/.test(c.text) && !/\broblox\b/.test(c.text); },
        respond: function (c) {
          if (/\bidea|ideas|concept\b/.test(c.text)) return { text: 'Game idea starters:\n- One-button runner\n- Match-3 with a twist\n- Reverse tower defense\n\nPick one small mechanic and build it first.' };
          if (/\bmechanic|mechanics\b/.test(c.text)) return { text: 'Core game-dev mechanics include movement, collision, state machines, timers/cooldowns, spawning, and object pooling. Which one do you want to learn?' };
          return { text: 'I can help with game mechanics, level design, project structure, Unity/Godot concepts, and game ideas. What are you building?' };
        }
      });
    }
  });

  ALLINONE.modules.register({
    id: 'roblox',
    name: 'Roblox Studio',
    description: 'Roblox Studio concepts, Luau scripting, game ideas for Roblox.',
    init: function (app) {
      app.brain.registerIntent({
        id: 'roblox.main', priority: 63,
        test: function (c) { return /\b(roblox|luau|roblox studio|ro-?blox)\b/.test(c.text); },
        respond: function (c) {
          if (/\bscript|scripting|luau|lua|code\b/.test(c.text)) return { text: 'Roblox Studio uses Luau. Good fundamentals include services, events, RemoteEvents, server/client separation, and validating important actions on the server. What mechanic are you scripting?' };
          if (/\bidea|ideas|game idea\b/.test(c.text)) return { text: 'Roblox ideas: an obby with a twist, a tycoon, a simulator, a round-based arena, or a social roleplay hub. What kind of game do you want to make?' };
          return { text: 'I can help with Roblox Studio, Luau scripting, Explorer/Properties, playtesting, game ideas, and project structure. What do you want to build?' };
        }
      });
    }
  });

  ALLINONE.modules.register({
    id: 'creative',
    name: 'Creative ideas',
    description: 'Brainstorming: names, stories, concepts, prompts.',
    init: function (app) {
      app.brain.registerIntent({
        id: 'creative.main', priority: 59,
        test: function (c) { return /\b(brainstorm|come up with|suggest|give me|idea|ideas|name|names|story|stories|prompt|prompts|concept|concepts)\b/.test(c.text) && /\b(name|names|idea|ideas|story|stories|prompt|prompts|concept|concepts|game|character|project|world|app)\b/.test(c.text); },
        respond: function (c) {
          if (/\bname|names\b/.test(c.text)) return { text: 'Name ideas: Nova, Ember, Ace, Pixel, Orion, Rune, Cobalt, Drift, Vector, Zenith. What is the name for?' };
          if (/\bstory|stories\b/.test(c.text)) return { text: 'A useful story engine is: character wants something → something blocks them → they change. What genre are you writing?' };
          if (/\bgame\b/.test(c.text)) return { text: 'Try combining two genres, adding one unusual rule, or starting from a feeling you want players to have. What kind of game are you imagining?' };
          return { text: 'I can brainstorm names, stories, prompts, game concepts, characters, projects, and worlds. What are you making?' };
        }
      });
    }
  });

  ALLINONE.modules.register({
    id: 'allinone-info',
    name: 'ALLINONE information',
    description: 'Questions about what ALLINONE is and how it works.',
    init: function (app) {
      app.brain.registerIntent({
        id: 'allinone-info.main', priority: 93,
        test: function (c) { return /\b(what(?:'s| is) allinone|about allinone|how does allinone work|is allinone (free|online|offline)|does allinone (use|need)|what is this (app|site|assistant)|how (?:is|are) (?:you|this) (?:built|made))\b/.test(c.text) || (/\ballinone\b/.test(c.text) && /\b(work|feature|built|free|offline|online|api|privacy|data)\b/.test(c.text)); },
        respond: function (c) { return { text: 'ALLINONE is a local-first assistant that runs in your browser. The project is designed around local JavaScript rules, local browser storage, no API keys, and no external AI service. Current assistant name: **' + c.assistantName + '**.' }; }
      });
    }
  });

  ALLINONE.modules.register({
    id: 'settings',
    name: 'Settings',
    description: 'Rename assistant, personality, user name, safe mode, clear chat, reset.',
    init: function () {}
  });
})(window);
