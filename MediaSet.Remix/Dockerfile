FROM node:20-alpine
RUN apk add --no-cache git
WORKDIR /usr/server/app

RUN git clone https://github.com/paulmfischer/MediaSet.git
WORKDIR /usr/server/app/MediaSet/MediaSet.Remix

COPY ./package.json ./
RUN npm install

COPY ./ .

RUN npm run build
ENV NODE_ENV=production

CMD ["npm", "run", "start"]