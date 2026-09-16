(function (global) {
  'use strict';

  var ALLINONE = (global.ALLINONE = global.ALLINONE || {});
  var config = ALLINONE.config;

  var intents = [];

  function registerIntent(intent) {
    if (!intent || typeof intent.test !== 'function' || typeof intent.respond !== 'function') {
      if (global.console) console.warn('[ALLINONE] Ignored invalid intent:', intent);
      return;
    }
    if (!intent.id) intent.id = 'intent-' + intents.length;
    if (typeof intent.priority !== 'number') intent.priority = 50;
    intents.push(intent);
    intents.sort(function (a, b) { return b.priority - a.priority; });
  }

  var memory = {
    userName: '',
    lastTopic: null
  };

  function norm(s) {
    return String(s == null ? '' : s)
      .toLowerCase()
      .replace(/[\u2018\u2019]/g, "'")
      .replace(/\s+/g, ' ')
      .trim();
  }

  function pick(arr) {
    return arr[Math.floor(Math.random() * arr.length)];
  }

  function stripEmoji(str) {
    return String(str)
      .replace(/[\u{1F300}-\u{1FAFF}\u2600-\u27BF\uFE0F]/gu, '')
      .replace(/[ \t]{2,}/g, ' ')
      .trim();
  }

  function MathParser(src) {
    this.src = String(src).replace(/\s+/g, '');
    this.pos = 0;
  }
  MathParser.prototype.peek = function () { return this.src[this.pos]; };
  MathParser.prototype.consume = function () { return this.src[this.pos++]; };
  MathParser.prototype.expect = function (ch) {
    if (this.peek() !== ch) throw new Error('Expected "' + ch + '"');
    this.pos++;
  };
  MathParser.prototype.parse = function () {
    var v = this.parseExpression();
    if (this.pos < this.src.length) throw new Error('Unexpected token');
    return v;
  };
  MathParser.prototype.parseExpression = function () {
    var v = this.parseTerm();
    while (this.peek() === '+' || this.peek() === '-') {
      var op = this.consume();
      var rhs = this.parseTerm();
      v = op === '+' ? v + rhs : v - rhs;
    }
    return v;
  };
  MathParser.prototype.parseTerm = function () {
    var v = this.parsePower();
    while (this.peek() === '*' || this.peek() === '/' || this.peek() === '%') {
      var op = this.consume();
      var rhs = this.parsePower();
      if (op === '*') v = v * rhs;
      else if (op === '/') {
        if (rhs === 0) throw new Error('Division by zero');
        v = v / rhs;
      } else {
        v = v % rhs;
      }
    }
    return v;
  };
  MathParser.prototype.parsePower = function () {
    var v = this.parseUnary();
    if (this.peek() === '^') {
      this.consume();
      var rhs = this.parsePower();
      v = Math.pow(v, rhs);
    }
    return v;
  };
  MathParser.prototype.parseUnary = function () {
    if (this.peek() === '-') { this.consume(); return -this.parseUnary(); }
    if (this.peek() === '+') { this.consume(); return this.parseUnary(); }
    return this.parsePrimary();
  };
  MathParser.prototype.parsePrimary = function () {
    if (this.peek() === '(') {
      this.consume();
      var v = this.parseExpression();
      this.expect(')');
      return v;
    }
    var start = this.pos;
    while (this.pos < this.src.length && /[0-9.]/.test(this.src[this.pos])) this.pos++;
    if (start === this.pos) throw new Error('Expected number');
    var num = parseFloat(this.src.slice(start, this.pos));
    if (!isFinite(num)) throw new Error('Invalid number');
    return num;
  };

  function calculate(expr) {
    return new MathParser(expr).parse();
  }

  function formatNumber(n) {
    if (!isFinite(n)) return String(n);
    return String(Math.round(n * 1e10) / 1e10);
  }

  function extractExpression(raw) {
    var s = String(raw == null ? '' : raw).toLowerCase().trim();
    s = s.replace(/^(what(?:'s| is)|whats|calculate|compute|solve|evaluate|eval|how much is|how many is)\s+/, '');
    s = s.replace(/[?=]/g, ' ').replace(/\s+/g, ' ').trim();

    if (!/\d/.test(s)) return null;
    if (!/[+\-*/%^]/.test(s)) return null;
    if (!/^[-+*/%^().\d\s]+$/.test(s)) return null;
    return s;
  }

  var TOPICS = [
    {
      id: 'space',
      words: ['space', 'planet', 'planets', 'galaxy', 'universe', 'star', 'stars', 'nasa', 'astronomy', 'mars', 'moon'],
      replies: [
        'Space is wild. A day on Venus is longer than its year, and there are more stars in the observable universe than grains of sand on Earth.\n\nWhat got you thinking about space?',
        'One of my favorite space facts: Jupiter\'s Great Red Spot is a storm that has been raging for centuries and is bigger than Earth.\n\nWant a fact about a specific planet?',
        'If you could visit anywhere in the solar system, where would you go? I\'d pick Titan — it has lakes, but they\'re made of liquid methane.'
      ]
    },
    {
      id: 'music',
      words: ['music', 'song', 'songs', 'band', 'album', 'guitar', 'piano', 'sing', 'singer', 'playlist'],
      replies: [
        'Music is one of the best focus tools there is. A lot of people work best with instrumental or lo-fi tracks because lyrics compete with reading and writing.\n\nWhat do you listen to while working?',
        'Fun fact: learning an instrument improves memory and timing because it trains your brain to process sound and movement together.\n\nDo you play anything?',
        'If you want to find new music, pick one artist you love and follow the "features" trail — you\'ll find a whole web of similar artists.'
      ]
    },
    {
      id: 'school',
      words: ['school', 'homework', 'study', 'studying', 'exam', 'test', 'class', 'teacher', 'college', 'revision'],
      replies: [
        'Study tip that actually works: test yourself instead of re-reading. Retrieval practice beats highlighting every single time.\n\nWhat subject are you working on?',
        'Try the 25/5 rhythm — 25 minutes of focus, 5 minutes off. Short bursts beat marathon sessions for most people.\n\nWhat\'s the hardest part for you right now?',
        'If something feels too big, break it into a first step so small it feels silly. Starting is the hard part, not continuing.'
      ]
    },
    {
      id: 'food',
      words: ['food', 'cook', 'cooking', 'recipe', 'pizza', 'snack', 'baking', 'dinner', 'lunch', 'breakfast'],
      replies: [
        'If you can boil water, you can cook. Start with three-ingredient meals and build up — pasta, eggs, and stir-fry are great training grounds.\n\nWhat do you like eating?',
        'Cooking rule of thumb: salt early, taste often, and don\'t crowd the pan. Those three fix most home cooking problems.\n\nWhat are you making?',
        'What\'s a food you\'d happily eat every week? I can help you think through a simple version of it.'
      ]
    },
    {
      id: 'animals',
      words: ['animal', 'animals', 'dog', 'dogs', 'cat', 'cats', 'pet', 'pets', 'bird', 'fish', 'horse'],
      replies: [
        'Animals are great. Octopuses have three hearts and blue blood, and they can taste with their arms.\n\nDo you have a pet?',
        'Dogs can learn well over a hundred words, and some learn by watching other dogs rather than being trained directly.\n\nWhat\'s your favorite animal?',
        'Cats purr at a frequency that\'s been shown to help with healing and stress. They\'re basically tiny vibrating health devices.'
      ]
    },
    {
      id: 'movies',
      words: ['movie', 'movies', 'film', 'films', 'cinema', 'series', 'show'],
      replies: [
        'Good stories usually work because the character wants something badly and something blocks them. That\'s basically it.\n\nWhat have you watched recently?',
        'If you\'re stuck picking something, pick by mood instead of genre — do you want comfort, tension, or a good cry?\n\nWhat mood are you in?',
        'A fun thing to try: rewatch a scene you love and count the cuts. Editing is invisible until you look for it.'
      ]
    },
    {
      id: 'sports',
      words: ['sport', 'sports', 'football', 'soccer', 'basketball', 'baseball', 'running', 'run', 'gym', 'workout'],
      replies: [
        'Consistency beats intensity almost every time. Twenty minutes you actually do beats two hours you keep postponing.\n\nWhat do you play or train for?',
        'Rest days are training days — that\'s when your body actually adapts. Skipping them is how people stall out.\n\nHow often do you train?',
        'What\'s the goal you\'re working toward? I can help you break it into something weekly.'
      ]
    },
    {
      id: 'books',
      words: ['book', 'books', 'reading', 'read', 'novel', 'author', 'writing'],
      replies: [
        'Reading tip: if a book doesn\'t grab you in 50 pages, it\'s fine to put it down. There are too many good books to push through bad ones.\n\nWhat are you reading?',
        'Want to write more? Lower the bar — 200 bad words a day beats waiting for inspiration.\n\nWhat kind of writing interests you?',
        'What was the last book that actually stuck with you?'
      ]
    },
    {
      id: 'art',
      words: ['art', 'draw', 'drawing', 'paint', 'painting', 'sketch'],
      replies: [
        'Drawing is mostly mileage. Do a small sketch every day and in a month you\'ll be shocked at the difference.\n\nWhat do you like to draw?',
        'A trick that helps a lot: flip your reference (and your drawing) upside-down. Your brain stops recognizing it and starts seeing shapes instead.\n\nWhat are you working on?',
        'What style of art do you want to get better at?'
      ]
    }
  ];

  function applyPersonality(text, personality) {
    var s = String(text == null ? '' : text);

    switch (personality) {
      case 'professional': {
        var t = stripEmoji(s).replace(/!+/g, '.');
        return t.replace(/^([a-z])/, function (m) { return m.toUpperCase(); });
      }

      case 'chill':
        if (s.length < 420 && Math.random() < 0.35) {
          s = s.replace(/!+/g, '.');
          if (Math.random() < 0.6) s = pick(['For sure. ', 'Yeah — ', 'Honestly, ', 'Cool. ']) + s;
        }
        return s;

      case 'energetic':
        if (s.length < 420) {
          if (!/[!?.]$/.test(s)) s += '!';
          if (Math.random() < 0.4) s = s.replace(/^/, pick(['Absolutely! ', 'Let\'s go! ', 'You got it! ']));
        }
        return s;

      default:
        return s;
    }
  }

  registerIntent({
    id: 'greeting',
    priority: 90,
    test: function (c) {
      return /^(hi|hey|hello|yo|sup|what'?s up|good morning|good afternoon|good evening)\b/.test(c.text);
    },
    respond: function (c) {
      var who = c.userName ? ', ' + c.userName : '';
      return { text: pick(['Hey' + who + '! How can I help?', 'Hi' + who + '! What are we working on?', 'Hello' + who + '! What can I do for you?']) };
    }
  });

  registerIntent({
    id: 'status',
    priority: 88,
    test: function (c) {
      return /^(how are you|how r u|you good|are you good|how\'s it going)\??$/.test(c.text);
    },
    respond: function (c) {
      if (c.personality === 'professional') return { text: 'Operating normally, thank you for asking. How are you?' };
      if (c.personality === 'chill') return { text: 'Yeah, all good over here. You?' };
      if (c.personality === 'energetic') return { text: 'Great, thanks for asking! Ready to help. How about you?' };
      return {
        text: pick([
          'Running smoothly, thanks for asking — I\'m all local, so no server hiccups here. How are you doing?',
          'Pretty good! I\'ve got everything I need right here on your device. How about you?',
          'Can\'t complain. What about you — how\'s your day going?'
        ])
      };
    }
  });

  registerIntent({
    id: 'thanks',
    priority: 82,
    test: function (c) {
      return /^(thanks|thank you|thx|ty|cheers|appreciate it)\b/.test(c.text);
    },
    respond: function () {
      return { text: pick(['Any time!', 'Happy to help.', 'No problem at all — that\'s what I\'m here for.']) };
    }
  });

  registerIntent({
    id: 'bye',
    priority: 82,
    test: function (c) {
      return /^(bye|goodbye|see ya|see you|good night|goodnight|talk to you later|later)\b/.test(c.text);
    },
    respond: function (c) {
      var who = c.userName ? ', ' + c.userName : '';
      return { text: pick(['See you later' + who + '!', 'Bye' + who + ' — come back any time.', 'Take care' + who + '!']) };
    }
  });

  registerIntent({
    id: 'joke',
    priority: 80,
    test: function (c) {
      return /\bjoke\b/.test(c.text) || /\bmake me laugh\b/.test(c.text) || /\bsomething funny\b/.test(c.text);
    },
    respond: function () {
      return {
        text: pick([
          'Why do programmers prefer dark mode?\nBecause light attracts bugs.',
          'I told my computer I needed a break.\nNow it won\'t stop sending me KitKat ads.',
          'Why did the developer go broke?\nBecause he used up all his cache.',
          'There are 10 types of people in the world: those who understand binary, and those who don\'t.',
          'Why don\'t scientists trust atoms?\nBecause they make up everything.',
          'I would tell you a UDP joke, but you might not get it.'
        ])
      };
    }
  });

  registerIntent({
    id: 'time',
    priority: 78,
    test: function (c) {
      return /\bwhat(?:'s| is)? the time\b/.test(c.text) ||
        /^time\b/.test(c.text) ||
        /\bcurrent time\b/.test(c.text) ||
        /\bwhat time is it\b/.test(c.text);
    },
    respond: function () {
      var now = new Date();
      var time = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
      return { text: 'It\'s **' + time + '** right now (your device\'s local time).' };
    }
  });

  registerIntent({
    id: 'date',
    priority: 78,
    test: function (c) {
      return /\bwhat(?:'s| is)? (?:the )?date\b/.test(c.text) ||
        /\btoday'?s date\b/.test(c.text) ||
        /\bwhat day is it\b/.test(c.text);
    },
    respond: function () {
      var now = new Date();
      var date = now.toLocaleDateString([], { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
      return { text: 'Today is **' + date + '**.' };
    }
  });

  registerIntent({
    id: 'math',
    priority: 76,
    test: function (c) { return extractExpression(c.raw) !== null; },
    respond: function (c) {
      var expr = extractExpression(c.raw);
      try {
        var result = calculate(expr);
        return { text: '`' + expr.replace(/\s+/g, ' ').trim() + '` = **' + formatNumber(result) + '**' };
      } catch (err) {
        return { text: 'I couldn\'t work that one out. Try something like `(12 * 4) + 7` or `2^10`.' };
      }
    }
  });

  registerIntent({
    id: 'more',
    priority: 74,
    test: function (c) {
      return /\b(tell me more|more about that|go on|continue|keep going)\b/.test(c.text);
    },
    respond: function () {
      if (memory.lastTopic) return { text: pick(memory.lastTopic.replies), topic: memory.lastTopic };
      return { text: 'More about what exactly? Give me a topic and I\'ll dig in.' };
    }
  });

  registerIntent({
    id: 'weather',
    priority: 72,
    test: function (c) {
      return /\bweather\b/.test(c.text) || /\bis it (?:going to )?rain/.test(c.text);
    },
    respond: function () {
      return {
        text:
          'I run completely offline, so I can\'t pull live weather — I have no internet connection at all.\n\n' +
          'That\'s actually a feature: nothing you type ever leaves your device. ' +
          'For weather, your device\'s built-in app will be much more accurate than I could be.'
      };
    }
  });

  registerIntent({
    id: 'maker',
    priority: 70,
    test: function (c) {
      return /\bwho (?:made|created|built|designed) you\b/.test(c.text) ||
        /\bwhere do you come from\b/.test(c.text);
    },
    respond: function (c) {
      return {
        text:
          'I\'m **' + c.assistantName + '**, an ALLINONE assistant. ' +
          'My "brain" is a set of rules and knowledge written in JavaScript that runs right here in your browser.\n\n' +
          'No cloud, no accounts, no API keys, no internet. You can add new skills to me over time through the module system.'
      };
    }
  });

  registerIntent({
    id: 'feelings',
    priority: 65,
    test: function (c) {
      return /\b(i'?m|i am|i feel|feeling)\s+(sad|down|depressed|lonely|alone|anxious|stressed|worried|upset|exhausted|overwhelmed|burnt out|burned out)\b/.test(c.text);
    },
    respond: function () {
      return {
        text: pick([
          'That sounds heavy, and I\'m sorry you\'re dealing with it.\n\nI\'m just a small offline assistant, but I\'m here to listen. Sometimes naming what\'s going on helps — what\'s weighing on you most right now?',
          'Thanks for telling me. That\'s a real thing to carry.\n\nIf it\'s been sitting with you a while, talking to someone you trust — a friend, family member, or counselor — tends to help more than any app can. Want to talk through what\'s going on?',
          'I hear you. Rough patches are real and they\'re not a personal failure.\n\nWhat would make today even slightly easier? Let\'s start there.'
        ])
      };
    }
  });

  registerIntent({
    id: 'topics',
    priority: 40,
    test: function (c) {
      for (var i = 0; i < TOPICS.length; i++) {
        var topic = TOPICS[i];
        for (var j = 0; j < topic.words.length; j++) {
          if (ALLINONE.safety.containsWord(c.text, topic.words[j])) {
            memory.lastTopic = topic;
            return true;
          }
        }
      }
      return false;
    },
    respond: function () {
      var topic = memory.lastTopic;
      if (!topic) return { text: 'Tell me more and I\'ll dig into it.' };
      return { text: pick(topic.replies), topic: topic };
    }
  });

  registerIntent({
    id: 'fallback',
    priority: 0,
    test: function () { return true; },
    respond: function (c) {
      if (/\?\s*$/.test(c.raw)) {
        return {
          text: pick([
            'I don\'t have that in my local knowledge. I run entirely offline — I\'m a small rule-based assistant, not a large cloud language model. I can help with greetings, math, time, date, jokes, coding, game dev, Roblox, and creative ideas.\n\nTell me more and I\'ll see what I can do.',
            'That\'s outside what my offline brain can handle, and I want to be honest about that. My knowledge is a small set of built-in rules that live entirely on your device.\n\nIf you rephrase it or give me context, I might be able to help.',
            'I can\'t answer that one — my knowledge base doesn\'t cover it. What I *can* do is help you break the question down. What part are you stuck on?'
          ])
        };
      }
      return {
        text: pick([
          'Got it — tell me more about that.',
          'Interesting. Say a bit more and I\'ll see what I can do.',
          'I\'m following. What else is going on with that?',
          'Noted. What would you like to do about it?'
        ])
      };
    }
  });

  ALLINONE.brain = {
    registerIntent: registerIntent,
    getIntents: function () { return intents.slice(); },

    setUserName: function (name) { memory.userName = String(name || '').trim(); },
    getUserName: function () { return memory.userName; },
    resetMemory: function () { memory.userName = ''; memory.lastTopic = null; },

    respond: function (input, ctx) {
      ctx = ctx || {};
      var personality = ctx.personality || config.DEFAULT_PERSONALITY;
      var text = norm(input);

      if (!text) return { text: 'Say something and I\'ll do my best.' };
      if (ctx.userName && !memory.userName) memory.userName = String(ctx.userName).trim();

      var context = {
        raw: String(input),
        text: text,
        assistantName: ctx.assistantName || config.DEFAULT_ASSISTANT_NAME,
        personality: personality,
        userName: ctx.userName || memory.userName || ''
      };

      for (var i = 0; i < intents.length; i++) {
        var intent = intents[i];
        var matched = false;
        try { matched = !!intent.test(context); }
        catch (err) {
          if (global.console) console.warn('[ALLINONE] Intent test failed:', intent.id, err);
          matched = false;
        }
        if (!matched) continue;

        var result;
        try { result = intent.respond(context); }
        catch (err) {
          if (global.console) console.error('[ALLINONE] Intent respond failed:', intent.id, err);
          continue;
        }
        if (!result) continue;
        if (typeof result === 'string') result = { text: result };
        if (result.topic) memory.lastTopic = result.topic;

        if (!intent.noTransform) result.text = applyPersonality(result.text, personality);
        return result;
      }

      return { text: 'I\'m not sure how to answer that yet.' };
    },

    calculate: calculate,
    formatNumber: formatNumber,
    pick: pick
  };
})(window);
