FROM node

WORKDIR /usr/src/app

COPY package*.json ./
COPY index.js ./

RUN npm install

CMD [ "npm", "/usr/src/app/index.js" ]